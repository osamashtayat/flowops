namespace FlowOps.Application.Common;

/// <summary>One page of a list (API_CONTRACT.md §1 <c>Paged&lt;T&gt;</c>).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
