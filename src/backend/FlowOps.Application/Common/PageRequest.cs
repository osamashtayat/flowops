using System.ComponentModel.DataAnnotations;

namespace FlowOps.Application.Common;

/// <summary>
/// Paging query parameters. Not sealed: list queries add their filters by deriving from it,
/// so the paging limits are declared once.
/// </summary>
public record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;
}
