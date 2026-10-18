using System.Text.Json.Nodes;

namespace Dreamsign.Contracts;

public sealed record ToolDefinition(string Name, string Description, JsonObject Schema, bool ReadOnly)
{
    public JsonObject ToJson() => new()
    {
        ["name"] = Name,
        ["description"] = Description,
        ["inputSchema"] = Schema.DeepClone(),
        ["annotations"] = new JsonObject { ["readOnlyHint"] = ReadOnly, ["openWorldHint"] = false }
    };
}

public static class ToolCatalog
{
    public const string Version = "0.6.0";
    public const string TestProjectPrefix = "Dreamsign_MCP_";
    public const string PipePrefix = "AstraRevitBridge-2027-";
    public const int RequestBytes = 262144, ResponseBytes = 1048576;
    private static readonly string[] Base = ["discover_instances", "get_project_state", "get_active_document", "ping_revit", "get_project_info", "get_levels", "query_elements", "validate_elements", "get_last_errors", "capture_snapshot", "read_snapshot", "diff_snapshots"];
    private static readonly Dictionary<string, string[]?> Profiles = new(StringComparer.Ordinal)
    {
        ["inspection"] = Base,
        ["workflow"] = [.. Base, "save_project", "run_workflow", "apply_plan", "set_window_sills", "assign_marks", "tag_rooms"],
        ["modeling"] = [.. Base, "save_project", "create_elements", "modify_elements", "delete_elements", "apply_plan", "set_window_sills", "assign_marks", "tag_rooms"],
        ["diagnostic"] = [.. Base, "create_test_project"],
        ["full"] = null
    };
    private static readonly JsonArray Definitions = Load();
    private static JsonArray Load()
    {
        using var stream = typeof(ToolCatalog).Assembly.GetManifestResourceStream("Dreamsign.Contracts.tool-catalog.json")
            ?? throw new InvalidOperationException("Missing embedded tool catalogue");
        return JsonNode.Parse(stream)!.AsArray();
    }
    public static IReadOnlyList<ToolDefinition> All => Definitions.Select(n => new ToolDefinition(
        n!["name"]!.GetValue<string>(), n["description"]!.GetValue<string>(),
        (JsonObject)n["inputSchema"]!.DeepClone(), n["annotations"]!["readOnlyHint"]!.GetValue<bool>())).ToArray();
    public static ToolDefinition Get(string name) => All.FirstOrDefault(t => t.Name == name)
        ?? throw new ArgumentException("Unknown tool: " + name);
    public static IReadOnlyList<ToolDefinition> ForProfile(string profile)
    {
        if (!Profiles.TryGetValue(profile, out var selected))
            throw new ArgumentException("Unknown MCP profile: " + profile);
        return selected is null ? All : All.Where(t => selected.Contains(t.Name, StringComparer.Ordinal)).ToArray();
    }
    public static IReadOnlyList<string> Commands => All.Where(t => t.Name is not ("discover_instances" or "get_project_state")).Select(t => t.Name).ToArray();
    public static JsonObject Error(string code, string message) => new() { ["success"] = false, ["code"] = code, ["message"] = message };
}
