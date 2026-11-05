namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>Base for tests that need the database: every test starts from empty data and a rewound clock.</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public abstract class DatabaseTest(FlowOpsApiFactory factory) : IAsyncLifetime
{
    protected FlowOpsApiFactory Factory { get; } = factory;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync() => await Factory.ResetAsync();

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
