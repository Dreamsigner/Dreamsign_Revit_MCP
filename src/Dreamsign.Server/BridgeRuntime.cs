using System.Text.Json.Nodes;
using Dreamsign.Contracts;

namespace Dreamsign.Server;

public sealed class BridgeRuntime(StateStore store, IBridgeTransport transport, string profile)
{
    private readonly HashSet<string> allowed = ToolCatalog.ForProfile(profile).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly HashSet<string> Features = ["capture_snapshot", "read_snapshot", "diff_snapshots", "preview_batch", "apply_plan", "set_window_sills", "assign_marks", "tag_rooms"];
    private static readonly HashSet<string> Recorded = ["create_elements", "modify_elements", "delete_elements", "execute_batch", "run_workflow", "save_project", "preview_batch", "apply_plan", "set_window_sills", "assign_marks", "tag_rooms"];

    public async Task<JsonObject> HandleAsync(string operation, JsonObject arguments, CancellationToken cancellationToken = default)
    {
        if (!allowed.Contains(operation)) throw new ArgumentException("Tool unavailable in the active profile: " + operation);
        SchemaValidator.Validate(arguments, ToolCatalog.Get(operation).Schema);
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var lease = store.AcquireLease();
            return await HandleCoreAsync(operation, arguments, cancellationToken);
        }
        finally { gate.Release(); }
    }

    private async Task<JsonObject> HandleCoreAsync(string operation, JsonObject arguments, CancellationToken cancellationToken)
    {
        if (operation == "get_project_state")
        {
            var state = store.Read();
            var localState = new JsonObject { ["success"] = true, ["mapping_count"] = state["mapping"]?.AsObject().Count ?? 0 };
            foreach (var key in new[] { "target", "decisions", "open_items", "baseline", "last_operation" }) localState[key] = state[key]?.DeepClone();
            return localState;
        }
        if (operation == "create_test_project")
        {
            var created = await transport.SendAsync(operation, (JsonObject)arguments.DeepClone(), new("", "", arguments["pid"]!.GetValue<int>()), cancellationToken);
            if (created["success"]?.GetValue<bool>() == true)
                try { store.Configure(new(created["document"]!.GetValue<string>(), created["path"]!.GetValue<string>(), created["pid"]!.GetValue<int>())); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { created["telemetry_warning"] = "Project created; local target could not be recorded: " + e.Message; }
            return created;
        }
        var target = store.Target;
        var found = await TargetResolver.ResolveAsync(target, transport, cancellationToken);
        if (operation == "discover_instances" || found["success"]?.GetValue<bool>() != true) return found;
        if (Features.Contains(operation) && found["bridge_version"]?.ToString() is not ("0.3.0" or "0.4.0" or ToolCatalog.Version)) return ToolCatalog.Error("UPGRADE_REQUIRED", "Load bridge 0.3 or later for this operation");
        var input = (JsonObject)arguments.DeepClone();
        var full = input["response_mode"]?.ToString() == "full";
        input.Remove("response_mode");
        var result = await transport.SendAsync(operation, input, target with { Pid = found["pid"]!.GetValue<int>() }, cancellationToken);
        if (result["code"]?.ToString() == "SAVE_FAILED") result["changes_applied"] = new[] { "created", "modified", "deleted" }.Sum(k => result[k]?.GetValue<int>() ?? 0) > 0;
        if (!Recorded.Contains(operation)) return result;
        string report;
        try { report = store.Record(operation, result); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException)
        {
            result["telemetry_warning"] = "Result received; local report/state could not be recorded: " + e.Message;
            return result;
        }
        return full ? result : Compact(result, report);
    }

    public static JsonObject Compact(JsonObject result, string report)
    {
        if (result["results"] is null && result["actions"] is null && result["before"] is null) return result;
        var summary = (JsonObject)result.DeepClone();
        var rows = result["results"]?.AsArray() ?? [];
        var errors = rows.Select((row, i) => (Row: row, Index: i)).Where(p => p.Row?["success"]?.GetValue<bool>() == false).ToArray();
        summary.Remove("results"); summary.Remove("actions"); summary.Remove("before");
        var shortErrors = new JsonArray();
        foreach (var error in errors.Take(5))
        {
            var row = (JsonObject)error.Row!.DeepClone();
            row["action_index"] ??= JsonValue.Create(error.Index);
            shortErrors.Add(row);
        }
        summary["result_count"] = rows.Count; summary["errors"] = shortErrors; summary["errors_truncated"] = errors.Length > 5; summary["detail_report"] = report;
        return summary;
    }
}
