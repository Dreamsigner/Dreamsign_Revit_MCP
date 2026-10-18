using System.Text.Json.Nodes;
using Dreamsign.Contracts;

namespace Dreamsign.Server;

public static class TargetResolver
{
    public static async Task<JsonObject> ResolveAsync(BridgeTarget target, IBridgeTransport transport, CancellationToken cancellationToken = default)
    {
        target.Validate();
        var pids = transport.EnumeratePids().Concat(target.Pid is int pid ? [pid] : Array.Empty<int>()).Where(p => p > 0).Distinct().ToArray();
        if (pids.Length > 8)
            throw new ArgumentException("Too many bridge instances; narrow the target");
        var matches = new JsonArray();
        var outdated = false;
        foreach (var candidate in pids)
        {
            var result = await transport.SendAsync("get_active_document", new(), target with
            {
                Pid = candidate
            }, cancellationToken);
            if (result["success"]?.GetValue<bool>() != true || result["document"]?.ToString() != target.Document)
                continue;
            var path = result["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
            {
                outdated = true;
                continue;
            }
            if (!Path.IsPathFullyQualified(path) || !string.Equals(Path.GetFullPath(path), Path.GetFullPath(target.Path), StringComparison.OrdinalIgnoreCase))
                continue;
            matches.Add(new JsonObject { ["pid"] = candidate, ["document"] = target.Document, ["path"] = path, ["bridge_version"] = result["bridge_version"]?.DeepClone() });
        }
        if (matches.Count > 1)
        {
            var ambiguous = ToolCatalog.Error("AMBIGUOUS_TARGET", "Multiple open instances match the configured project; no write executed");
            ambiguous["instances"] = matches;
            return ambiguous;
        }
        if (matches.Count == 0)
            return ToolCatalog.Error(outdated ? "UPGRADE_REQUIRED" : "TARGET_UNAVAILABLE", outdated ? "Load a bridge that reports the document path" : "Open the configured project " + target.Document + " in Revit 2027");
        var found = (JsonObject)matches[0]!.DeepClone();
        found["success"] = true;
        return found;
    }
}
