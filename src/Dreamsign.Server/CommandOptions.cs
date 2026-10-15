using Dreamsign.Contracts;

namespace Dreamsign.Server;

public sealed record CommandOptions(string Command, string DataDirectory, string Profile, IReadOnlyDictionary<string, string> Values, bool ReplaceExisting)
{
    public string Require(string key) => Values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Missing --" + key);
    public static CommandOptions Parse(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal)) return new("help", "", "workflow", new Dictionary<string, string>(), false);
        var command = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : "serve";
        if (!new[] { "serve", "configure", "doctor", "install", "uninstall", "catalog" }.Contains(command)) throw new ArgumentException("Unknown command: " + command);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var replace = false;
        for (var i = command == "serve" && (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal)) ? 0 : 1; i < args.Length; i++)
        {
            if (args[i] == "--replace-existing") { if (replace) throw new ArgumentException("Duplicate --replace-existing"); replace = true; continue; }
            var key = args[i].StartsWith("--", StringComparison.Ordinal) ? args[i][2..] : "";
            if (!new[] { "data-dir", "profile", "document", "path", "pid", "plugin-dir" }.Contains(key) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Invalid option: " + args[i]);
            if (!values.TryAdd(key, args[++i])) throw new ArgumentException("Duplicate option: " + key);
        }
        var allowed = command switch
        {
            "configure" => new[] { "data-dir", "document", "path", "pid" },
            "install" => new[] { "data-dir", "plugin-dir" },
            "serve" or "catalog" => ["data-dir", "profile"],
            _ => ["data-dir"]
        };
        if (values.Keys.Any(k => !allowed.Contains(k)) || (replace && command != "install")) throw new ArgumentException("Option does not apply to this command");
        var profile = values.GetValueOrDefault("profile") ?? Environment.GetEnvironmentVariable("ASTRA_MCP_PROFILE") ?? "workflow";
        _ = ToolCatalog.ForProfile(profile);
        var dir = values.GetValueOrDefault("data-dir") ?? Environment.GetEnvironmentVariable("DREAMSIGN_MCP_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DreamsignRevitMCP");
        return new(command, Path.GetFullPath(dir), profile, values, replace);
    }
}
