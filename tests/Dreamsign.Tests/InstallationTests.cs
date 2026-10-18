using System.Xml.Linq;
using Dreamsign.Server;
using Xunit;

namespace Dreamsign.Tests;

public sealed class InstallationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Dreamsign.Tests", Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    [Fact]
    public void InstallEscapesPathsAndReplacementRequiresExplicitFlag()
    {
        var plugin = Path.Combine(directory, "input");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(Path.Combine(plugin, "Dreamsign.Revit.dll"), "test");
        File.WriteAllText(Path.Combine(plugin, "Dreamsign.Contracts.dll"), "test");
        File.WriteAllText(Path.Combine(plugin, "Dreamsign.Desktop.dll"), "test");
        File.WriteAllText(Path.Combine(plugin, "Tomlyn.dll"), "test");
        var manifests = Path.Combine(directory, "manifests");
        var data = Path.Combine(directory, "data & files");
        var result = Installation.Install(plugin, manifests, data);
        var manifest = result["manifest"]!.GetValue<string>();
        var original = File.ReadAllText(manifest);
        Assert.Equal(result["assembly"]!.GetValue<string>(), XDocument.Load(manifest).Root!.Element("AddIn")!.Element("Assembly")!.Value);
        Assert.Throws<InvalidOperationException>(() => Installation.Install(plugin, manifests, data));
        Assert.Equal(original, File.ReadAllText(manifest));
        var replaced = Installation.Install(plugin, manifests, data, true);
        Assert.Equal(original, File.ReadAllText(replaced["backup"]!.GetValue<string>()));
        Assert.True(Installation.Uninstall(manifests, data)["removed"]!.GetValue<bool>());
        Assert.False(File.Exists(manifest));
        Assert.True(File.Exists(result["assembly"]!.GetValue<string>()));
    }

    [Fact]
    public void UninstallRefusesUnrecognizedManifest()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Installation.ManifestName);
        File.WriteAllText(path, "<RevitAddIns><AddIn><FullClassName>Other.App</FullClassName></AddIn></RevitAddIns>");
        Assert.Throws<InvalidOperationException>(() => Installation.Uninstall(directory, directory));
        Assert.True(File.Exists(path));
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("serve", "--profile", "missing")]
    [InlineData("serve", "--profile", "inspection", "--profile", "full")]
    [InlineData("configure", "--path")]
    public void InvalidCliOptionsAreRejected(params string[] args) => Assert.Throws<ArgumentException>(() => CommandOptions.Parse(args));
}
