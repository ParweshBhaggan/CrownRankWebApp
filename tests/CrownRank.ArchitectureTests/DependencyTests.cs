using NetArchTest.Rules;

namespace CrownRank.ArchitectureTests;

public sealed class DependencyTests
{
    [Fact]
    public void Domain_ShouldNotDependOnApplication()
    {
        var result = Types.InAssembly(typeof(Domain.Creators.Creator).Assembly)
            .ShouldNot()
            .HaveDependencyOn("CrownRank.Application")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}

