namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>
/// Test classes in this collection share one <see cref="FlowOpsApiFactory"/> (one PostgreSQL container)
/// and run one after another, so a per-test reset is safe.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollectionDefinition : ICollectionFixture<FlowOpsApiFactory>
{
    public const string Name = "Database";
}
