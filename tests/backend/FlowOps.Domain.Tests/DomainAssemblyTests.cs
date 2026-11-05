using System.Reflection;

namespace FlowOps.Domain.Tests;

// Placeholder until BE-06 adds the domain model: proves the project wiring and test runner work.
public sealed class DomainAssemblyTests
{
    [Fact]
    public void DomainAssembly_IsLoadable()
    {
        var assembly = Assembly.Load("FlowOps.Domain");

        Assert.Equal("FlowOps.Domain", assembly.GetName().Name);
    }
}
