using CrownRank.Domain.Creators;
using NetArchTest.Rules;
using Xunit;

namespace CrownRank.ArchitectureTests;

public sealed class DependencyTests
{
    [Fact]
    public void Domain_ShouldNotDependOnApplication()
    {
        var result = Types.InAssembly(typeof(Creator).Assembly)
            .ShouldNot()
            .HaveDependencyOn("CrownRank.Application")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
