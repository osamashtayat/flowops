namespace FlowOps.Api.IntegrationTests.Architecture;

public sealed class ConfigurationRulesTests
{
    [Fact]
    public void CommittedAppSettings_ContainNoConnectionStrings()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src", "backend"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        files.ShouldNotBeEmpty();
        files.Where(f => File.ReadAllText(f).Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty("connection strings come from environment variables or user-secrets only");
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FlowOps.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("FlowOps.slnx not found.");
    }
}
