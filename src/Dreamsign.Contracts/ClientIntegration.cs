using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tomlyn;
using Tomlyn.Model;

namespace Dreamsign.Contracts;

public enum DesktopClient { Codex, ClaudeDesktop, Cursor }
public sealed record ClientConfigurationResult(string Path, string? Backup, string EntryName);

public static class ClientIntegration
{
    public const string EntryName = "dreamsign_revit";
    public static string ConfigurationPath(DesktopClient client) => client switch
    {
        DesktopClient.Codex => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "config.toml"),
        DesktopClient.ClaudeDesktop => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude_desktop_config.json"),
        DesktopClient.Cursor => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json"),
        _ => throw new ArgumentOutOfRangeException(nameof(client))
    };
    public static string[] Arguments(string dataDirectory, string profile) => ["serve", "--profile", profile, "--data-dir", Path.GetFullPath(dataDirectory)];

    public static ClientConfigurationResult Configure(DesktopClient client, string path, string executable, string dataDirectory, string profile)
    {
        if (!Path.IsPathFullyQualified(executable) || Path.GetFileName(executable) != "Dreamsign.Revit.Mcp.exe" || !File.Exists(executable))
            throw new ArgumentException("Le serveur installé est introuvable. Relancez l’installation.");
        _ = ToolCatalog.ForProfile(profile);
        using var lease = new StateStore(dataDirectory).AcquireLease();
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        var (updated, entry) = Transform(client, text, executable, Arguments(dataDirectory, profile), remove: false);
        var backup = Write(path, updated, dataDirectory);
        var receipts = Receipt(dataDirectory);
        receipts[client.ToString()] = new JsonObject { ["path"] = Path.GetFullPath(path), ["executable"] = executable };
        AtomicFile.Write(Path.Combine(dataDirectory, "clients.json"), receipts.ToJsonString());
        return new(path, backup, entry);
    }

    public static bool Remove(DesktopClient client, string path, string executable, string dataDirectory)
    {
        if (!File.Exists(path)) return false;
        var text = File.ReadAllText(path);
        var (updated, entry) = Transform(client, text, executable, [], remove: true);
        if (entry == "") return false;
        Write(path, updated, dataDirectory);
        return true;
    }

    public static void RemoveRegistered(string dataDirectory, string executable)
    {
        var receipts = Receipt(dataDirectory);
        foreach (var client in Enum.GetValues<DesktopClient>())
            if (receipts[client.ToString()] is JsonObject receipt && receipt["executable"]?.ToString() == executable)
            {
                // Only standard client locations are writable during unattended removal.
                var path = ConfigurationPath(client);
                if (string.Equals(receipt["path"]?.ToString(), path, StringComparison.OrdinalIgnoreCase))
                    Remove(client, path, executable, dataDirectory);
            }
    }

    private static JsonObject Receipt(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, "clients.json");
        return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))!.AsObject() : new();
    }
    private static (string Text, string Entry) Transform(DesktopClient client, string text, string executable, string[] args, bool remove)
    {
        if (client == DesktopClient.Codex)
        {
            if (Toml.Parse(text).HasErrors) throw new ArgumentException("Configuration TOML invalide ; le fichier existant est conservé.");
            var root = Toml.ToModel(text);
            if (root.TryGetValue("mcp_servers", out var value) && value is not TomlTable) throw new InvalidOperationException("Configuration MCP invalide.");
            var servers = value as TomlTable ?? new TomlTable();
            var key = servers.ContainsKey(EntryName) ? EntryName : servers.TryGetValue("astra_revit", out var legacy) && legacy is TomlTable old && Owned(old.TryGetValue("command", out var oldCommand) ? oldCommand?.ToString() : null, old.TryGetValue("args", out var a) ? a as System.Collections.IEnumerable : null) ? "astra_revit" : EntryName;
            var existing = servers.TryGetValue(key, out var found) ? found as TomlTable ?? throw new InvalidOperationException("Entrée MCP invalide.") : new TomlTable();
            existing.TryGetValue("command", out var command); existing.TryGetValue("args", out var arguments);
            if (remove)
            {
                if (!string.Equals(command?.ToString(), executable, StringComparison.OrdinalIgnoreCase)) return (text, "");
                servers.Remove(key);
            }
            else
            {
                if (found is not null && !Owned(command?.ToString(), arguments as System.Collections.IEnumerable))
                    throw new InvalidOperationException("Cette entrée appartient à un autre serveur ; aucun remplacement effectué.");
                existing["command"] = executable;
                var array = new TomlArray(); foreach (var arg in args) array.Add(arg);
                existing["args"] = array; existing["enabled"] = true; servers[key] = existing;
            }
            root["mcp_servers"] = servers;
            return (Toml.FromModel(root), key);
        }
        var json = string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
        var entries = json["mcpServers"]?.AsObject() ?? new JsonObject();
        var name = entries.ContainsKey(EntryName) ? EntryName : entries["astra_revit"] is JsonObject oldJson && Owned(oldJson["command"]?.ToString(), oldJson["args"]?.AsArray()) ? "astra_revit" : EntryName;
        var server = entries[name]?.AsObject() ?? new JsonObject();
        if (remove)
        {
            if (!string.Equals(server["command"]?.ToString(), executable, StringComparison.OrdinalIgnoreCase)) return (text, "");
            entries.Remove(name);
        }
        else
        {
            if (entries.ContainsKey(name) && !Owned(server["command"]?.ToString(), server["args"]?.AsArray()))
                throw new InvalidOperationException("Cette entrée appartient à un autre serveur ; aucun remplacement effectué.");
            server["command"] = executable; server["args"] = new JsonArray(args.Select(a => (JsonNode)JsonValue.Create(a)!).ToArray()); entries[name] = server;
        }
        if (json["mcpServers"] is null) json["mcpServers"] = entries;
        return (json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), name);
    }
    private static bool Owned(string? command, System.Collections.IEnumerable? args)
    {
        if (Path.GetFileName(command) == "Dreamsign.Revit.Mcp.exe") return true;
        return Path.GetFileName(command) is "node" or "node.exe" && args is not null &&
            args.Cast<object?>().Any(a => a?.ToString()?.Replace('\\', '/').EndsWith("/AstraRevitBridge/server.mjs", StringComparison.OrdinalIgnoreCase) == true);
    }
    private static string? Write(string path, string text, string dataDirectory)
    {
        string? backup = null;
        if (File.Exists(path))
        {
            var directory = Path.Combine(dataDirectory, "backups"); Directory.CreateDirectory(directory);
            backup = Path.Combine(directory, "Dreamsign_MCP_" + Guid.NewGuid().ToString("N") + Path.GetExtension(path));
            File.Copy(path, backup);
        }
        AtomicFile.Write(path, text);
        return backup;
    }
}

public static class AtomicFile
{
    public static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            if (OperatingSystem.IsWindows() && File.Exists(path))
                System.IO.FileSystemAclExtensions.SetAccessControl(new FileInfo(temporary), System.IO.FileSystemAclExtensions.GetAccessControl(new FileInfo(path)));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public static class LocalSettings
{
    public static string DefaultDirectory => Environment.GetEnvironmentVariable("DREAMSIGN_MCP_DATA_DIR") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DreamsignRevitMCP");
    public static string? InstalledServer(string dataDirectory) => InstalledServer(dataDirectory, MachineServer());
    public static string? InstalledServer(string dataDirectory, string? machineServer) =>
        ValidServer(machineServer) ? machineServer : UserInstalledServer(dataDirectory);
    public static string? MachineServer()
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var machine = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
        using var key = machine.OpenSubKey(@"SOFTWARE\Dreamsigner\RevitMCP");
        var executable = key?.GetValue("Server") as string;
        return ValidServer(executable) ? executable : null;
    }
    public static string? UserInstalledServer(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, "installation.json");
        if (!File.Exists(path)) return null;
        var executable = JsonNode.Parse(File.ReadAllText(path))?["executable"]?.ToString();
        return ValidServer(executable) ? executable : null;
    }
    private static bool ValidServer(string? executable) => executable is not null && Path.IsPathFullyQualified(executable)
        && Path.GetFileName(executable) == "Dreamsign.Revit.Mcp.exe" && File.Exists(executable);
}
