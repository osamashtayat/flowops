namespace FlowOps.Api.IntegrationTests.Architecture;

public sealed class ConfigurationRulesTests
{
    [Fact]
    public void CommittedAppSettings_ContainNoConnectionStrings()
    {
        var root = TestPaths.RepositoryRoot;
        var files = Directory.GetFiles(Path.Combine(root, "src", "backend"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        files.ShouldNotBeEmpty();
        files.Where(f => File.ReadAllText(f).Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty("connection strings come from environment variables or user-secrets only");
    }
}
