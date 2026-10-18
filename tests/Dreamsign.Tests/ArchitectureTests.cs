using Xunit;
using System.Xml.Linq;

namespace Dreamsign.Tests;

public sealed class ArchitectureTests
{
    [Theory]
    [InlineData("Dreamsign.Contracts", "")]
    [InlineData("Dreamsign.Server", "Dreamsign.Contracts")]
    [InlineData("Dreamsign.Desktop", "Dreamsign.Contracts")]
    [InlineData("Dreamsign.Revit", "Dreamsign.Contracts,Dreamsign.Desktop")]
    public void ProjectReferencesRespectComponentBoundaries(string project, string allowed)
    {
        var root = RepositoryRoot();
        var file = Path.Combine(root, "src", project, project + ".csproj");
        var references = XDocument.Load(file).Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value))
            .Order(StringComparer.Ordinal).ToArray();
        var expected = allowed.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, references);
    }

    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Dreamsign.Revit.MCP.sln")))
                return directory.FullName;

        throw new DirectoryNotFoundException("Cannot find the repository root.");
    }
}
