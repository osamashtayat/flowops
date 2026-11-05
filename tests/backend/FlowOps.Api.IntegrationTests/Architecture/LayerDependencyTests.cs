using System.Reflection;
using System.Xml.Linq;

namespace FlowOps.Api.IntegrationTests.Architecture;

/// <summary>
/// Enforces the dependency table in docs/ARCHITECTURE.md §3.
/// Checks both the declared references in each .csproj (the compiler drops unused project
/// references from the assembly metadata, so metadata alone would miss them) and the
/// compiled assembly references (which also catch transitive leaks into a lower layer).
/// </summary>
public sealed class LayerDependencyTests
{
    private const string Domain = "FlowOps.Domain";
    private const string Application = "FlowOps.Application";
    private const string Infrastructure = "FlowOps.Infrastructure";
    private const string Api = "FlowOps.Api";

    /// <summary>Exactly the project references each layer has — no more, no fewer.</summary>
    private static readonly Dictionary<string, string[]> AllowedProjectReferences = new()
    {
        [Domain] = [],
        [Application] = [Domain],
        [Infrastructure] = [Application, Domain],
        [Api] = [Application, Infrastructure],
    };

    /// <summary>Assembly-name prefixes a layer must never reference (directly or by compiled metadata).</summary>
    private static readonly Dictionary<string, string[]> ForbiddenAssemblyPrefixes = new()
    {
        [Domain] = [Application, Infrastructure, Api, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions", "Npgsql"],
        [Application] = [Infrastructure, Api, "Microsoft.AspNetCore", "Npgsql"],
        [Infrastructure] = [Api],
        [Api] = [],
    };

    public static TheoryData<string> Layers => [.. AllowedProjectReferences.Keys];

    [Fact]
    public void EveryBackendProject_IsCoveredByTheDependencyTable()
    {
        var projects = Directory
            .GetFiles(Path.Combine(TestPaths.RepositoryRoot, "src", "backend"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedProjectReferences.Keys.Order(StringComparer.Ordinal), projects);
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void ProjectReferences_MatchArchitectureTableExactly(string project)
    {
        var actual = ReadProject(project)
            .Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(NormalizePath(e.Attribute("Include")!.Value)))
            .Order(StringComparer.Ordinal);

        Assert.Equal(AllowedProjectReferences[project].Order(StringComparer.Ordinal), actual);
    }

    [Fact]
    public void Domain_HasNoPackageReferences()
    {
        var packages = PackageReferences(Domain);

        Assert.Empty(packages);
    }

    [Fact]
    public void Application_ReferencesOnlyEfCoreAndExtensionsAbstractions()
    {
        var disallowed = PackageReferences(Application)
            .Where(p => !IsAllowedApplicationPackage(p))
            .ToArray();

        Assert.Empty(disallowed);
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void CompiledAssembly_DoesNotReferenceForbiddenAssemblies(string project)
    {
        var violations = Assembly.Load(project)
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => ForbiddenAssemblyPrefixes[project].Any(prefix => IsSameOrChild(name, prefix)))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CompiledDomainAssembly_ReferencesOnlyTheBcl()
    {
        var nonBcl = Assembly.Load(Domain)
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => !IsBcl(name))
            .ToArray();

        Assert.Empty(nonBcl);
    }

    private static bool IsAllowedApplicationPackage(string package) =>
        package == "Microsoft.EntityFrameworkCore"
        || (package.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            && package.EndsWith(".Abstractions", StringComparison.Ordinal));

    private static bool IsBcl(string assemblyName) =>
        assemblyName is "netstandard" or "mscorlib" or "System"
        || assemblyName.StartsWith("System.", StringComparison.Ordinal);

    private static bool IsSameOrChild(string name, string prefix) =>
        name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal);

    private static string[] PackageReferences(string project) =>
        [.. ReadProject(project).Descendants("PackageReference").Select(e => e.Attribute("Include")!.Value)];

    private static XDocument ReadProject(string project) =>
        XDocument.Load(Path.Combine(TestPaths.RepositoryRoot, "src", "backend", project, project + ".csproj"));

    private static string NormalizePath(string path) => path.Replace('\\', Path.DirectorySeparatorChar);
}
