namespace FlowOps.Application.Common;

/// <summary>
/// The Application layer's unit of work (ADR-0007). Only use-case services use it, and they call
/// <see cref="SaveChangesAsync"/> exactly once per use case. Entity sets are added as their
/// mappings land (BE-07); there is deliberately no generic repository.
/// </summary>
public interface IAppDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
