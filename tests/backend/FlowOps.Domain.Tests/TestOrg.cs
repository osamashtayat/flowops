using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Tests;

/// <summary>A small fixed organisation and helpers for driving the aggregate.</summary>
internal static class TestOrg
{
    public static readonly Guid Requester = new("01900000-0000-7000-8000-000000000001");
    public static readonly Guid Manager = new("01900000-0000-7000-8000-000000000002");
    public static readonly Guid FinanceOfficer = new("01900000-0000-7000-8000-000000000003");
    public static readonly Guid HrOfficer = new("01900000-0000-7000-8000-000000000004");
    public static readonly Guid ProcurementOfficer = new("01900000-0000-7000-8000-000000000005");
    public static readonly Guid DepartmentId = new("01900000-0000-7000-8000-0000000000d1");

    public static readonly DateTimeOffset T0 = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    public static readonly TimeZoneInfo Amman = TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman");

    /// <summary>Stands in for the Application layer's TaskAssigneeResolver.</summary>
    public static Assignee Resolve(ProcessStep step) =>
        step.Assignee.IsRequesterManager ? Assignee.ForUser(Manager) : Assignee.ForRole(step.Assignee.Role!);

    public static PurchaseDetails Laptop() =>
        PurchaseDetails.Create("Laptop", "14-inch developer laptop", Money.Create(1250.500m, SupportedCurrencies.Jod), "Current one fails daily");

    public static LeaveDetails AnnualLeave() =>
        LeaveDetails.Create(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12), LeaveType.Annual, "Family visit", T0, Amman);

    public static Request SubmitPurchase(Guid? requester = null) =>
        Request.SubmitPurchase(RequestNumber.Create(2026, 1), requester ?? Requester, DepartmentId, Laptop(), Manager, T0);

    public static Request SubmitLeave() =>
        Request.SubmitLeave(RequestNumber.Create(2026, 2), Requester, DepartmentId, AnnualLeave(), Manager, T0);

    public static DecisionResult ApproveCurrent(this Request request, Guid actor, DateTimeOffset? at = null) =>
        request.DecideTask(request.OpenTask!.Id, actor, TaskDecision.Approve(null), Resolve, at ?? T0);

    public static DecisionResult RejectCurrent(this Request request, Guid actor, string reason, DateTimeOffset? at = null) =>
        request.DecideTask(request.OpenTask!.Id, actor, TaskDecision.Reject(reason), Resolve, at ?? T0);

    public static DecisionResult FulfillCurrent(this Request request, Guid actor, DateTimeOffset? at = null) =>
        request.DecideTask(request.OpenTask!.Id, actor, TaskDecision.Fulfill(null), Resolve, at ?? T0);

    /// <summary>The activities appended since <paramref name="countBefore"/> entries existed.</summary>
    public static RequestActivity[] AppendedSince(this Request request, int countBefore) =>
        [.. request.Activities.Skip(countBefore)];

    public static void ShouldFailWith(Action action, string code) =>
        Should.Throw<DomainException>(action).Code.ShouldBe(code);
}
