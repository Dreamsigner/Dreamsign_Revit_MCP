using System.Text.Json.Nodes;
using System.Xml.Linq;
using Dreamsign.Contracts;
using Dreamsign.Server;
using Xunit;

namespace Dreamsign.Tests;

public sealed class MachineInstallationTests : IDisposable
{
    [Fact]
    public void Revit2027MachineManifestIsUnderProgramFiles()
    {
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit", "Addins", "2027"), Installation.MachineManifestDirectory);
        Assert.False(Installation.MachineManifestDirectory.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), StringComparison.OrdinalIgnoreCase));
    }
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Dreamsign_MCP_" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    private string Exe(string folder)
    {
        var path = Path.Combine(directory, folder, "Dreamsign.Revit.Mcp.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "fixture"); return path;
    }
    [Fact]
    public void SharedServerWinsWithoutReadingOrChangingPersonalState()
    {
        var machine = Exe("machine"); var data = Path.Combine(directory, "user"); Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "installation.json"), "invalid legacy receipt");
        File.WriteAllText(Path.Combine(data, "project-state.json"), "private target");
        Assert.Equal(machine, LocalSettings.InstalledServer(data, machine));
        Assert.Equal("private target", File.ReadAllText(Path.Combine(data, "project-state.json")));
        Assert.Equal("invalid legacy receipt", File.ReadAllText(Path.Combine(data, "installation.json")));
    }
    [Fact]
    public void MissingSharedServerFallsBackToTheExistingUserInstallation()
    {
        var legacy = Exe("legacy"); var data = Path.Combine(directory, "user"); Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "installation.json"), new JsonObject { ["executable"] = legacy }.ToJsonString());
        Assert.Equal(legacy, LocalSettings.InstalledServer(data, Path.Combine(directory, "missing", "Dreamsign.Revit.Mcp.exe")));
        Assert.Equal(legacy, LocalSettings.UserInstalledServer(data));
    }
    [Fact]
    public void MigrationBacksUpOnlyOwnedManifestAndPreservesProjectAndClientFiles()
    {
        var f = MigrationFixture(); var data = Path.Combine(directory, "user"); Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "project-state.json"), "private target");
        File.WriteAllText(Path.Combine(data, "clients.json"), "private clients");
        var original = File.ReadAllText(f.UserManifest);
        var result = UserMigration.Prepare(Path.GetDirectoryName(f.UserManifest)!, data, f.Server, f.SharedManifest);
        Assert.Equal(original, File.ReadAllText(result["backup"]!.ToString()));
        Assert.False(File.Exists(f.UserManifest)); Assert.True(File.Exists(f.SharedManifest));
        Assert.Equal("private target", File.ReadAllText(Path.Combine(data, "project-state.json")));
        Assert.Equal("private clients", File.ReadAllText(Path.Combine(data, "clients.json")));
        Assert.False(UserMigration.Prepare(Path.GetDirectoryName(f.UserManifest)!, data, f.Server, f.SharedManifest)["migrated"]!.GetValue<bool>());
    }
    [Fact]
    public void MigrationRefusesForeignManifestAndIncompleteSharedInstallation()
    {
        var f = MigrationFixture(); var data = Path.Combine(directory, "user");
        File.WriteAllText(f.UserManifest, "<RevitAddIns><AddIn><FullClassName>Foreign.App</FullClassName></AddIn></RevitAddIns>");
        var original = File.ReadAllText(f.UserManifest);
        Assert.Throws<InvalidOperationException>(() => UserMigration.Prepare(Path.GetDirectoryName(f.UserManifest)!, data, f.Server, f.SharedManifest));
        Assert.Equal(original, File.ReadAllText(f.UserManifest));
        File.Delete(f.SharedManifest);
        Assert.Throws<InvalidOperationException>(() => UserMigration.Prepare(Path.GetDirectoryName(f.UserManifest)!, data, f.Server, f.SharedManifest));
        Assert.Equal(original, File.ReadAllText(f.UserManifest));
    }
    private (string Server, string UserManifest, string SharedManifest) MigrationFixture()
    {
        var server = Exe("machine");
        var plugin = Path.Combine(Path.GetDirectoryName(server)!, "addin", "Dreamsign.Revit.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(plugin)!); File.WriteAllText(plugin, "fixture");
        string Manifest(string folder)
        {
            var path = Path.Combine(directory, folder, Installation.ManifestName); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            new XDocument(new XElement("RevitAddIns", new XElement("AddIn", new XAttribute("Type", "Application"),
                new XElement("AddInId", Installation.AddInId), new XElement("FullClassName", "AstraRevitBridge.App"),
                new XElement("Assembly", plugin)))).Save(path); return path;
        }
        return (server, Manifest("legacy-manifests"), Manifest("shared-manifests"));
    }
}
