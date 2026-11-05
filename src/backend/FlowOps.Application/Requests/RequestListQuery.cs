using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FlowOps.Application.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary><c>GET /api/requests</c> query. Sorted by submission time, newest first.</summary>
public sealed record RequestListQuery : PageRequest
{
    public const int SearchMaxLength = 100;

    public RequestScope Scope { get; init; } = RequestScope.Mine;

    public RequestStatus? Status { get; init; }

    public RequestType? Type { get; init; }

    /// <summary>Request number prefix or item name.</summary>
    [StringLength(SearchMaxLength)]
    public string? Search { get; init; }
}

/// <summary><c>mine</c>: own requests. <c>visible</c>: everything the caller may see (managers' "Team requests").</summary>
public enum RequestScope
{
    [JsonStringEnumMemberName("mine")]
    Mine,

    [JsonStringEnumMemberName("visible")]
    Visible,
}
