using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dreamsign.Contracts;

namespace Dreamsign.Contracts;

public sealed class StateStore(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string StatePath => Path.Combine(DirectoryPath, "project-state.json");
    public FileStream AcquireLease()
    {
        Directory.CreateDirectory(DirectoryPath);
        // Fail before dispatch if another process is operating on the same local state.
        return new FileStream(Path.Combine(DirectoryPath, ".operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public JsonObject Read()
    {
        if (!File.Exists(StatePath))
            throw new InvalidOperationException("Configure a target first: Dreamsign.Revit.Mcp configure --document TITLE --path ABSOLUTE_RVT_PATH");
        return JsonNode.Parse(File.ReadAllText(StatePath))!.AsObject();
    }
    public BridgeTarget Target => BridgeTarget.FromJson(Read()["target"]!.AsObject());
    public void SelectProject(string document, string path, int pid, bool isFamily)
    {
        if (isFamily || !Path.IsPathFullyQualified(path) || !path.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new ArgumentException("Ouvrez un projet Revit enregistré avant de le sélectionner.");
        using var lease = AcquireLease();
        Configure(new(document, Path.GetFullPath(path), pid));
    }
    public void Configure(BridgeTarget target)
    {
        target.Validate();
        var state = File.Exists(StatePath) ? Read() : new JsonObject
        {
            ["version"] = 1,
            ["decisions"] = new JsonObject { ["units"] = "meters" },
            ["open_items"] = new JsonArray(),
            ["baseline"] = new JsonObject { ["live"] = false, ["counts"] = new JsonObject() },
            ["mapping"] = new JsonObject()
        };
        // Mapping and baseline belong to one exact target and cannot migrate to a different model.
        if (state["target"]?["document"]?.ToString() != target.Document ||
            !string.Equals(state["target"]?["path"]?.ToString(), target.Path, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(StatePath))
                Backup();
            state["mapping"] = new JsonObject();
            state["baseline"] = new JsonObject { ["live"] = false, ["counts"] = new JsonObject() };
            state.Remove("last_operation");
        }
        state["target"] = target.ToJson();
        Save(state);
    }
    public void Backup()
    {
        if (File.Exists(StatePath))
            File.Copy(StatePath, Path.Combine(DirectoryPath, "project-state.before-" + Guid.NewGuid().ToString("N") + ".json"));
    }
    public void Save(JsonObject state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, state.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(temp, StatePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public string Record(string operation, JsonObject result)
    {
        var id = Guid.TryParse(result["request_id"]?.ToString(), out var parsed) ? parsed : Guid.NewGuid();
        var reports = Path.Combine(DirectoryPath, "reports");
        Directory.CreateDirectory(reports);
        var path = Path.Combine(reports, id.ToString("D") + ".json");
        File.WriteAllText(path, new JsonObject { ["utc"] = DateTime.UtcNow.ToString("o"), ["operation"] = operation, ["result"] = result.DeepClone() }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var state = Read();
        state["last_operation"] = new JsonObject { ["utc"] = DateTime.UtcNow.ToString("o"), ["operation"] = operation, ["success"] = result["success"]?.DeepClone(), ["request_id"] = result["request_id"]?.DeepClone(), ["detail_report"] = path };
        var mapping = state["mapping"]?.AsObject() ?? new JsonObject();
        if (state["mapping"] is null)
            state["mapping"] = mapping;
        if (result["rolled_back"]?.GetValue<bool>() != true && result["status"]?.ToString() != "preview")
        {
            foreach (var row in result["results"]?.AsArray() ?? [])
            {
                if (row?["success"]?.GetValue<bool>() != true)
                    continue;
                if (row["source_id"] is JsonNode source && row["unique_id"] is JsonNode uid)
                    mapping[source.ToString()] = new JsonObject { ["unique_id"] = uid.DeepClone(), ["kind"] = row["class"]?.DeepClone() };
                if (row["status"]?.ToString() == "deleted")
                    foreach (var key in mapping.Where(p => p.Value?["unique_id"]?.ToString() == row["identifier"]?.ToString()).Select(p => p.Key).ToArray())
                        mapping.Remove(key);
            }
            if (result["success"]?.GetValue<bool>() == true && operation == "run_workflow")
                state["mapping_status"] = "refresh_required_after_category_delete";
        }
        Save(state);
        return path;
    }
}
