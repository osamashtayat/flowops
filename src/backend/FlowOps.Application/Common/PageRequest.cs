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

    /// <summary>Keeps the offset ((page − 1) × pageSize) far from int overflow, which used to surface as a 500.</summary>
    public const int MaxPage = 10_000;

    [Range(1, MaxPage)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;
}
