using System.Security.Principal;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Dreamsign.Contracts;

namespace Dreamsign.Server;

/// <summary>Explicit, unelevated removal of a legacy user override; never an MSI custom action.</summary>
public static class UserMigration
{
    public static void RequireStandardUser()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows est requis.");
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.IsSystem || new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Lancez la migration depuis votre session habituelle, sans exécuter en tant qu’administrateur.");
    }
    public static JsonObject Prepare(string userManifests, string data, string sharedServer, string sharedManifest)
    {
        if (!Path.IsPathFullyQualified(sharedServer) || Path.GetFileName(sharedServer) != "Dreamsign.Revit.Mcp.exe"
            || !File.Exists(sharedServer) || !File.Exists(sharedManifest) || !Installation.IsOwned(sharedManifest, true))
            throw new InvalidOperationException("L’installation commune complète doit être présente avant la migration.");
        using (var reader = XmlReader.Create(sharedManifest, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            var assembly = XDocument.Load(reader).Root!.Element("AddIn")!.Element("Assembly")?.Value ?? "";
            var expected = Path.Combine(Path.GetDirectoryName(sharedServer)!, "addin", "Dreamsign.Revit.dll");
            if (!Path.IsPathFullyQualified(assembly) || !string.Equals(Path.GetFullPath(assembly), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase) || !File.Exists(expected))
                throw new InvalidOperationException("Le manifeste commun ne pointe pas vers le complément installé.");
        }
        using var lease = new StateStore(data).AcquireLease();
        var manifest = Path.Combine(userManifests, Installation.ManifestName);
        if (!File.Exists(manifest))
            return new()
            {
                ["success"] = true,
                ["migrated"] = false
            };
        if (!Installation.IsOwned(manifest, true))
            throw new InvalidOperationException("Le manifeste utilisateur n’appartient pas à cette installation C# ; aucun remplacement effectué.");
        var backups = Path.Combine(data, "backups");
        Directory.CreateDirectory(backups);
        var backup = Path.Combine(backups, "Dreamsign_MCP_" + Guid.NewGuid().ToString("N") + ".addin");
        File.Copy(manifest, backup);
        if (!File.ReadAllBytes(manifest).SequenceEqual(File.ReadAllBytes(backup)))
            throw new IOException("Le manifeste a changé pendant la migration ; il est conservé.");
        File.Delete(manifest);
        return new()
        {
            ["success"] = true,
            ["migrated"] = true,
            ["backup"] = backup,
            ["restart_required"] = true,
            ["next_step"] = "Au prochain lancement de Revit, ouvrez Dreamsign MCP et configurez votre assistant. Aucun projet ni réglage client n’a été modifié."
        };
    }
}
