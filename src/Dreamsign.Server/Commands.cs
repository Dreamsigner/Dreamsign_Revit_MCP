using System.Text.Json;
using System.Text.Json.Nodes;
using Dreamsign.Contracts;

namespace Dreamsign.Server;

public static class Commands
{
    public static async Task<int> RunAsync(CommandOptions options, StateStore store)
    {
        JsonNode result;
        switch (options.Command)
        {
            case "connect":
                if (!Enum.TryParse<DesktopClient>(options.Require("client"), true, out var client) || !Enum.IsDefined(client)) throw new ArgumentException("Client inconnu : Codex, ClaudeDesktop ou Cursor attendu.");
                var connection = ClientIntegration.Configure(client, ClientIntegration.ConfigurationPath(client),
                    Path.Combine(AppContext.BaseDirectory,"Dreamsign.Revit.Mcp.exe"), store.DirectoryPath, options.Profile);
                result = new JsonObject { ["success"] = true, ["configuration"] = connection.Path, ["backup"] = connection.Backup };
                break;
            case "setup-install":
                result = SetupLifecycle.Register(Path.Combine(AppContext.BaseDirectory,"addin"), options.Values.GetValueOrDefault("manifest-dir") ?? Installation.DefaultManifestDirectory,
                    store.DirectoryPath, Path.Combine(AppContext.BaseDirectory,"Dreamsign.Revit.Mcp.exe"));
                break;
            case "setup-rollback":
                result = SetupLifecycle.Rollback(options.Values.GetValueOrDefault("manifest-dir") ?? Installation.DefaultManifestDirectory, store.DirectoryPath);
                break;
            case "setup-commit":
                result = SetupLifecycle.Commit(store.DirectoryPath);
                break;
            case "setup-uninstall":
                result = SetupLifecycle.Unregister(options.Values.GetValueOrDefault("manifest-dir") ?? Installation.DefaultManifestDirectory,store.DirectoryPath,Path.Combine(AppContext.BaseDirectory,"Dreamsign.Revit.Mcp.exe"));
                break;
            case "configure":
                using (var lease = store.AcquireLease())
                {
                    var file = options.Require("path");
                    if (!Path.IsPathFullyQualified(file) || !file.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) || !File.Exists(file)) throw new ArgumentException("Target must be an existing RVT with an absolute path");
                    int? pid = null;
                    if (options.Values.TryGetValue("pid", out var value)) pid = int.TryParse(value, out var parsed) && parsed > 0 ? parsed : throw new ArgumentException("PID must be positive");
                    var target = new BridgeTarget(options.Require("document"), Path.GetFullPath(file), pid);
                    store.Configure(target);
                    result = new JsonObject { ["success"] = true, ["state_file"] = store.StatePath, ["target"] = target.ToJson() };
                }
                break;
            case "doctor":
                result = await new BridgeRuntime(store, new NamedPipeTransport(), "inspection").HandleAsync("get_active_document", new());
                break;
            case "catalog":
                result = new JsonArray(ToolCatalog.ForProfile(options.Profile).Select(t => (JsonNode)t.ToJson()).ToArray());
                break;
            case "install":
                result = Installation.Install(options.Values.GetValueOrDefault("plugin-dir") ?? Path.Combine(AppContext.BaseDirectory, "addin"),
                    options.Values.GetValueOrDefault("manifest-dir") ?? Installation.DefaultManifestDirectory, store.DirectoryPath, options.ReplaceExisting);
                break;
            case "uninstall":
                result = Installation.Uninstall(options.Values.GetValueOrDefault("manifest-dir") ?? Installation.DefaultManifestDirectory, store.DirectoryPath);
                break;
            default: throw new ArgumentException("Unknown command");
        }
        Console.WriteLine(result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return result is JsonObject obj && obj["success"]?.GetValue<bool>() == false ? 1 : 0;
    }
}
