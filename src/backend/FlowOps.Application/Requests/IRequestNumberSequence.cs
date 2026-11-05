namespace FlowOps.Application.Requests;

/// <summary>Port to the database sequence behind <c>Request.Number</c> (DOMAIN_MODEL.md §2.3). Gaps are fine; duplicates never happen.</summary>
public interface IRequestNumberSequence
{
    Task<long> NextAsync(CancellationToken ct);
}
