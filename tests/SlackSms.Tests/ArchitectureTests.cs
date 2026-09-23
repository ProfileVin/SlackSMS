using System.Reflection;
using SlackSms.Application;
using SlackSms.Domain;

namespace SlackSms.Tests;

/// <summary>Guards the Clean Architecture dependency rule: inner layers never reference outer ones.</summary>
public class ArchitectureTests
{
    private static string[] ReferencedAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Fact]
    public void Domain_references_no_other_project_or_framework_extension()
    {
        var references = ReferencedAssemblies(typeof(ChannelMessage).Assembly);

        Assert.DoesNotContain(references, r => r.StartsWith("SlackSms", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => r.StartsWith("Microsoft.", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_references_only_domain_and_extension_abstractions()
    {
        var references = ReferencedAssemblies(typeof(BridgeOptions).Assembly);

        Assert.Equal(["SlackSms.Domain"], references.Where(r => r.StartsWith("SlackSms", StringComparison.Ordinal)));
        Assert.DoesNotContain(references, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, r => r.StartsWith("System.Net.Http", StringComparison.Ordinal));
    }
}
