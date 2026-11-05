namespace FlowOps.Api.IntegrationTests;

internal static class TestPaths
{
    private static readonly Lazy<string> Root = new(() =>
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FlowOps.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("FlowOps.slnx not found above " + AppContext.BaseDirectory);
    });

    /// <summary>The repository root (the folder holding FlowOps.slnx).</summary>
    public static string RepositoryRoot => Root.Value;
}
