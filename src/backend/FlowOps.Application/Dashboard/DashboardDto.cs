using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Dashboard;

/// <summary><c>GET /api/dashboard</c>. Sections the caller lacks the capability for are null.</summary>
public sealed record DashboardDto(
    MyRequestsSummaryDto MyRequests,
    MyTasksSummaryDto? MyTasks,
    IReadOnlyList<DashboardActivityDto> RecentActivity,
    AdminSummaryDto? Admin);

/// <summary><see cref="Recent"/> holds at most 5 requests.</summary>
public sealed record MyRequestsSummaryDto(int InReview, int Approved, int Completed, int Rejected, IReadOnlyList<RequestSummaryDto> Recent);

/// <summary>Present only with the <c>tasks.view</c> capability.</summary>
public sealed record MyTasksSummaryDto(int Open, DateTimeOffset? OldestOpenCreatedAt, int DecidedLast7Days);

/// <summary>An activity entry plus the request it belongs to; at most 10, on requests visible to the caller.</summary>
public sealed record DashboardActivityDto(
    Guid Id,
    ActivityType Type,
    UserRef? Actor,
    RequestStage? Stage,
    string? Body,
    DateTimeOffset OccurredAt,
    Guid RequestId,
    string RequestNumber);

/// <summary>Present only with the <c>admin.manage</c> capability.</summary>
public sealed record AdminSummaryDto(int ActiveUsers, int Departments, int StalledTasks);
