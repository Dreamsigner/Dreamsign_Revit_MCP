using System.Text.Json.Nodes;
using Dreamsign.Contracts;
using Dreamsign.Server;
using Xunit;
namespace Dreamsign.Tests;

public sealed class SetupLifecycleTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Dreamsign_MCP_" + Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
    private (string Plugin, string Manifests, string Data, string Exe) Fixture()
    {
        var plugin = Path.Combine(directory, "plugin");
        Directory.CreateDirectory(plugin);
        foreach (var file in new[] { "Dreamsign.Revit.dll", "Dreamsign.Contracts.dll", "Dreamsign.Desktop.dll", "Tomlyn.dll" })
            File.WriteAllText(Path.Combine(plugin, file), "fixture");
        var exe = Path.Combine(directory, "Dreamsign.Revit.Mcp.exe");
        File.WriteAllText(exe, "fixture");
        return (plugin, Path.Combine(directory, "manifests"), Path.Combine(directory, "data"), exe);
    }
    [Fact]
    public void FailedInstallationRollsBackManifestAndReceipt()
    {
        var f = Fixture();
        SetupLifecycle.Register(f.Plugin, f.Manifests, f.Data, f.Exe, false);
        SetupLifecycle.Commit(f.Data);
        var manifest = Path.Combine(f.Manifests, Installation.ManifestName);
        var before = File.ReadAllText(manifest);
        var receipt = File.ReadAllText(Path.Combine(f.Data, "installation.json"));
        SetupLifecycle.Register(f.Plugin, f.Manifests, f.Data, f.Exe, false);
        Assert.NotEqual(before, File.ReadAllText(manifest));
        Assert.True(SetupLifecycle.Rollback(f.Manifests, f.Data)["restored"]!.GetValue<bool>());
        Assert.Equal(before, File.ReadAllText(manifest));
        Assert.Equal(receipt, File.ReadAllText(Path.Combine(f.Data, "installation.json")));
    }
    [Fact]
    public void RemovalOfPreviousVersionCannotUnregisterNewInstallation()
    {
        var f = Fixture();
        SetupLifecycle.Register(f.Plugin, f.Manifests, f.Data, f.Exe, false);
        SetupLifecycle.Commit(f.Data);
        var result = SetupLifecycle.Unregister(f.Manifests, f.Data, Path.Combine(directory, "old", "Dreamsign.Revit.Mcp.exe"));
        Assert.False(result["removed"]!.GetValue<bool>());
        Assert.True(File.Exists(Path.Combine(f.Manifests, Installation.ManifestName)));
        Assert.True(SetupLifecycle.Unregister(f.Manifests, f.Data, f.Exe)["removed"]!.GetValue<bool>());
    }
    [Fact]
    public void RollbackRefusesToOverwriteAChangedManifest()
    {
        var f = Fixture();
        SetupLifecycle.Register(f.Plugin, f.Manifests, f.Data, f.Exe, false);
        var manifest = Path.Combine(f.Manifests, Installation.ManifestName);
        File.WriteAllText(manifest, "changed");
        Assert.Throws<InvalidOperationException>(() => SetupLifecycle.Rollback(f.Manifests, f.Data));
        Assert.Equal("changed", File.ReadAllText(manifest));
    }
}
