using System.Xml.Linq;
using Xunit;

namespace Shared.Tests;

public sealed class ServiceBoundaryTests
{
    [Fact]
    public void Service_projects_only_reference_their_own_context_and_building_blocks_or_contracts()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "B2BOrderBridge.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var services = Path.Combine(root.FullName, "src", "Services");
        foreach (var service in Directory.GetDirectories(services))
        foreach (var project in Directory.GetFiles(service, "*.csproj", SearchOption.AllDirectories))
        foreach (var reference in XDocument.Load(project).Descendants("ProjectReference"))
        {
            var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference.Attribute("Include")!.Value));
            var relativeToServices = Path.GetRelativePath(services, target);
            if (!relativeToServices.StartsWith("..", StringComparison.Ordinal))
                Assert.True(target.StartsWith(service + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                    $"Cross-service project reference: {project} -> {target}");
        }
    }
}
