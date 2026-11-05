using System.Reflection;
using Commerce.Domain;

namespace Commerce.UnitTests.Architecture;

public sealed class DependencyDirectionTests
{
    private static readonly Assembly Domain = DomainAssembly.Reference;
    private static readonly Assembly Application = typeof(global::Commerce.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(global::Commerce.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_other_commerce_projects()
    {
        Assert.Empty(CommerceReferences(Domain));
    }

    [Fact]
    public void Application_depends_only_on_domain()
    {
        Assert.All(CommerceReferences(Application), name => Assert.Equal("Commerce.Domain", name));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        Assert.DoesNotContain("Commerce.Api", CommerceReferences(Infrastructure));
    }

    [Fact]
    public void Domain_and_application_do_not_reference_infrastructure_frameworks()
    {
        string[] forbidden = ["Microsoft.EntityFrameworkCore", "Npgsql", "AWSSDK", "Microsoft.AspNetCore"];

        foreach (var assembly in new[] { Domain, Application })
        {
            var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);
            Assert.DoesNotContain(references, name => forbidden.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
        }
    }

    private static string[] CommerceReferences(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("Commerce.", StringComparison.Ordinal))];
}
