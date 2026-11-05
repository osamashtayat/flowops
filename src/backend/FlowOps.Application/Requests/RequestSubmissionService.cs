using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Application.Notifications;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>
/// Submits purchase and leave requests (API_CONTRACT.md §4, WORKFLOWS.md §2–3). Requester, department and time come
/// from the server (I14); the manager must be assigned and active (I12). One transaction: the request, its first task,
/// its activities and the manager's notification are saved together.
/// </summary>
public sealed class RequestSubmissionService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IRequestNumberSequence numbers,
    CompanyCalendar calendar,
    TimeProvider time,
    AccessRules access,
    RequestDetailsReader details)
{
    public async Task<RequestDetailsDto> SubmitPurchaseAsync(SubmitPurchaseRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var (requester, departmentId, managerId) = await LoadSubmitterAsync(ct);
        var purchase = PurchaseDetails.Create(body.ItemName, body.Description, body.EstimatedCost!.ToMoney("estimatedCost"), body.Justification);
        var now = time.GetUtcNow();
        var request = Request.SubmitPurchase(await NextNumberAsync(now, ct), requester.Id, departmentId, purchase, managerId, now);
        return await SaveAsync(request, requester, managerId, now, ct);
    }

    public async Task<RequestDetailsDto> SubmitLeaveAsync(SubmitLeaveRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var (requester, departmentId, managerId) = await LoadSubmitterAsync(ct);
        var now = time.GetUtcNow();
        var leave = LeaveDetails.Create(body.StartDate!.Value, body.EndDate!.Value, body.LeaveType!.Value, body.Reason, now, calendar.TimeZone);
        var request = Request.SubmitLeave(await NextNumberAsync(now, ct), requester.Id, departmentId, leave, managerId, now);
        return await SaveAsync(request, requester, managerId, now, ct);
    }

    /// <summary>Department first (422 <c>request.department_not_assigned</c>), then the manager (422 <c>request.manager_not_assigned</c>).</summary>
    private async Task<(TaskAssigneeResolver.Requester Requester, Guid DepartmentId, Guid ManagerId)> LoadSubmitterAsync(CancellationToken ct)
    {
        var requester = await TaskAssigneeResolver.LoadRequesterAsync(db, currentUser.Id, ct)
            ?? throw new AuthenticationFailedException(ErrorCodes.AuthUnauthenticated, "Your session has ended. Please sign in again.");
        var departmentId = requester.DepartmentId
            ?? throw new BusinessRuleException(ErrorCodes.RequestDepartmentNotAssigned,
                "You are not in a department yet. Ask an administrator to assign one.");
        return (requester, departmentId, TaskAssigneeResolver.RequireManager(requester));
    }

    /// <summary>Taken only after all input checks passed, so invalid submissions don't burn numbers.</summary>
    private async Task<RequestNumber> NextNumberAsync(DateTimeOffset now, CancellationToken ct) =>
        RequestNumber.Create(calendar.YearOf(now), await numbers.NextAsync(ct));

    private async Task<RequestDetailsDto> SaveAsync(
        Request request, TaskAssigneeResolver.Requester requester, Guid managerId, DateTimeOffset now, CancellationToken ct)
    {
        db.Requests.Add(request);
        db.Notifications.AddRange(NotificationFactory.TaskAssigned(request, request.OpenTask!, requester.DisplayName, [managerId], now));
        await db.SaveChangesAsync(ct);
        // Realtime "task assigned" push lands with IRealtimePublisher (BE-14); clients refetch meanwhile.
        var me = await access.ForCurrentUserAsync(ct);
        return await details.ReadAsync(request.Id, me, ct)
            ?? throw new InvalidOperationException("A request the caller just submitted must be visible to them.");
    }
}
