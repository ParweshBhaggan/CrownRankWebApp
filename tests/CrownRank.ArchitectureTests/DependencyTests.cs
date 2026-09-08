using CrownRank.Domain.Creators;
using CrownRank.Application.Creators;
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

    [Fact]
    public void Domain_ShouldNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(typeof(Creator).Assembly).ShouldNot()
            .HaveDependencyOnAny("CrownRank.Application", "CrownRank.Infrastructure", "CrownRank.Api").GetResult();
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(CreatorService).Assembly).ShouldNot()
            .HaveDependencyOnAny("CrownRank.Infrastructure", "CrownRank.Api").GetResult();
        Assert.True(result.IsSuccessful);
    }
}
