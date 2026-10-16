using System.Text.Json.Nodes;
using System.Xml.Linq;
using Dreamsign.Contracts;
using Microsoft.Win32;

namespace Dreamsign.Server;

/// <summary>MSI integration; Windows Installer owns the payload and invokes these reversible steps.</summary>
public static class SetupLifecycle
{
    private static string Journal(string data) => Path.Combine(data, "setup-" + ToolCatalog.Version + ".rollback.json");
    public static JsonObject Register(string plugin, string manifests, string data, string executable, bool checkRevit = true)
    {
        if (checkRevit && !RevitInstalled()) throw new InvalidOperationException("Revit 2027 est introuvable sur ce poste.");
        using var lease = new StateStore(data).AcquireLease();
        var manifest = Path.Combine(manifests, Installation.ManifestName);
        if (File.Exists(manifest) && !Installation.IsOwned(manifest)) throw new InvalidOperationException("Le manifeste existant appartient à un autre complément.");
        var receipt = Path.Combine(data, "installation.json");
        var snapshot = new JsonObject
        {
            ["before_manifest"] = File.Exists(manifest) ? File.ReadAllText(manifest) : null,
            ["before_receipt"] = File.Exists(receipt) ? File.ReadAllText(receipt) : null
        };
        AtomicFile.Write(Journal(data), snapshot.ToJsonString());
        var result = Installation.Install(plugin, manifests, data, true);
        snapshot["installed_manifest"] = File.ReadAllText(manifest);
        AtomicFile.Write(Journal(data), snapshot.ToJsonString());
        AtomicFile.Write(receipt, new JsonObject { ["executable"] = Path.GetFullPath(executable), ["version"] = ToolCatalog.Version }.ToJsonString());
        return result;
    }
    public static JsonObject Rollback(string manifests, string data)
    {
        using var lease = new StateStore(data).AcquireLease();
        if (!File.Exists(Journal(data))) return new() { ["success"] = true, ["restored"] = false };
        var snapshot = JsonNode.Parse(File.ReadAllText(Journal(data)))!.AsObject();
        var manifest = Path.Combine(manifests, Installation.ManifestName);
        var current = File.Exists(manifest) ? File.ReadAllText(manifest) : null;
        if (current != snapshot["installed_manifest"]?.ToString() && current != snapshot["before_manifest"]?.ToString())
            throw new InvalidOperationException("Le manifeste a changé après installation ; restauration automatique refusée.");
        Restore(manifest, snapshot["before_manifest"]?.ToString());
        Restore(Path.Combine(data, "installation.json"), snapshot["before_receipt"]?.ToString());
        File.Delete(Journal(data));
        return new() { ["success"] = true, ["restored"] = true };
    }
    public static JsonObject Commit(string data)
    {
        using var lease = new StateStore(data).AcquireLease();
        if (File.Exists(Journal(data))) File.Delete(Journal(data));
        return new() { ["success"] = true };
    }
    public static JsonObject Unregister(string manifests, string data, string executable)
    {
        using var lease = new StateStore(data).AcquireLease();
        var receipt = Path.Combine(data, "installation.json");
        if (LocalSettings.UserInstalledServer(data) is not string installed || !string.Equals(installed, executable, StringComparison.OrdinalIgnoreCase))
            return new() { ["success"] = true, ["removed"] = false };
        var result = Installation.Uninstall(manifests, data);
        try { ClientIntegration.RemoveRegistered(data, executable); }
        catch (Exception error) when (error is IOException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { result["configuration_warning"] = "La configuration modifiée par l’utilisateur est conservée : " + error.Message; }
        File.Delete(receipt);
        return result;
    }
    private static void Restore(string path, string? value)
    {
        if (value is null) { if (File.Exists(path)) File.Delete(path); }
        else AtomicFile.Write(path, value);
    }
    private static bool RevitInstalled()
    {
        if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2027", "Revit.exe"))) return true;
        if (!OperatingSystem.IsWindows()) return false;
        foreach (var key in new[] { @"SOFTWARE\Autodesk\Revit\2027", @"SOFTWARE\Autodesk\Revit\Autodesk Revit 2027" })
        {
            using var registry = Registry.LocalMachine.OpenSubKey(key);
            foreach (var name in new[] { "InstallationLocation", "InstallLocation", "InstallationPath" })
                if (registry?.GetValue(name) is string path && File.Exists(Path.Combine(path, "Revit.exe"))) return true;
        }
        return false;
    }
}
