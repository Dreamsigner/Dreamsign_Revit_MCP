using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Dreamsign.Contracts;

namespace Dreamsign.Server;

public static class Installation
{
    public const string ManifestName = "AstraRevitBridge.addin";
    public const string AddInId = "82C55A6F-5AB5-4A4B-A8E0-4C0E4E6B08C1";
    public static string DefaultManifestDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "Revit", "Addins", "2027");

    public static JsonObject Install(string pluginDirectory, string manifestDirectory, string dataDirectory, bool replaceExisting = false)
    {
        string[] files = ["Dreamsign.Revit.dll", "Dreamsign.Contracts.dll", "Dreamsign.Desktop.dll", "Tomlyn.dll"];
        foreach (var file in files)
            if (!File.Exists(Path.Combine(pluginDirectory, file))) throw new ArgumentException("Missing plugin file: " + file);
        var manifest = Path.Combine(manifestDirectory, ManifestName);
        if (File.Exists(manifest))
        {
            if (!IsOwned(manifest)) throw new InvalidOperationException("Existing manifest belongs to an unrecognized add-in; installation stopped");
            if (!replaceExisting) throw new InvalidOperationException("An existing bridge is installed. Use --replace-existing to back up and replace its manifest");
        }
        var destination = Path.GetFullPath(Path.Combine(dataDirectory, "addin", ToolCatalog.Version + "-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(destination);
        foreach (var file in files) File.Copy(Path.Combine(pluginDirectory, file), Path.Combine(destination, file));
        Directory.CreateDirectory(manifestDirectory);
        var backup = File.Exists(manifest) ? Backup(manifest, dataDirectory) : null;
        var xml = new XDocument(new XElement("RevitAddIns", new XElement("AddIn", new XAttribute("Type", "Application"),
            new XElement("Name", "Dreamsign Revit MCP"), new XElement("Assembly", Path.Combine(destination, files[0])),
            new XElement("AddInId", AddInId), new XElement("FullClassName", "AstraRevitBridge.App"),
            new XElement("VendorId", "DRMS"), new XElement("VendorDescription", "Dreamsigner"))));
        var temporary = manifest + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { xml.Save(temporary); File.Move(temporary, manifest, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return new() { ["success"] = true, ["manifest"] = manifest, ["assembly"] = Path.Combine(destination, files[0]), ["backup"] = backup, ["restart_required"] = true };
    }

    public static JsonObject Uninstall(string manifestDirectory, string dataDirectory)
    {
        var manifest = Path.Combine(manifestDirectory, ManifestName);
        if (!File.Exists(manifest)) return new() { ["success"] = true, ["removed"] = false };
        if (!IsOwned(manifest, requireNewAssembly: true)) throw new InvalidOperationException("Manifest is not this C# installation; no files removed");
        var backup = Backup(manifest, dataDirectory);
        File.Delete(manifest);
        return new() { ["success"] = true, ["removed"] = true, ["backup"] = backup, ["restart_required"] = true };
    }

    internal static bool IsOwned(string manifest, bool requireNewAssembly = false)
    {
        using var reader = XmlReader.Create(manifest, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var entries = XDocument.Load(reader).Root?.Elements("AddIn").ToArray();
        return entries is { Length: 1 } && Guid.TryParse(entries[0].Element("AddInId")?.Value, out var id) && id == Guid.Parse(AddInId)
            && entries[0].Element("FullClassName")?.Value == "AstraRevitBridge.App"
            && (!requireNewAssembly || Path.GetFileName(entries[0].Element("Assembly")?.Value) == "Dreamsign.Revit.dll");
    }

    private static string Backup(string manifest, string dataDirectory)
    {
        var directory = Path.Combine(dataDirectory, "backups");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".addin");
        File.Copy(manifest, path);
        return path;
    }
}
