using CrownRank.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Tests
{
    public sealed class DomainArchitectureTests
    {
        [Fact]
        public void Domain_DoesNotReferenceOuterLayersOrInfrastructureFrameworks()
        {
            var assembly =
                typeof(Entity).Assembly;

            var referencedAssemblies =
                assembly
                    .GetReferencedAssemblies()
                    .Select(reference => reference.Name)
                    .Where(name => name is not null)
                    .ToList();

            var forbiddenPrefixes =
                new[]
                {
                "CrownRank.Application",
                "CrownRank.Infrastructure",
                "CrownRank.API",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Stripe"
                };

            foreach (var forbiddenPrefix in forbiddenPrefixes)
            {
                Assert.DoesNotContain(
                    referencedAssemblies,
                    assemblyName =>
                        assemblyName!.StartsWith(
                            forbiddenPrefix,
                            StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
