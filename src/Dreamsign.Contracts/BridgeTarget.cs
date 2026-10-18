using System.Text.Json.Nodes;

namespace Dreamsign.Contracts;

public sealed record BridgeTarget(string Document, string Path, int? Pid = null)
{
    public static BridgeTarget FromJson(JsonObject target) => new(
        target["document"]?.GetValue<string>() ?? "", target["path"]?.GetValue<string>() ?? "", target["pid"]?.GetValue<int>());
    public JsonObject ToJson() => new() { ["document"] = Document, ["path"] = Path, ["pid"] = Pid };
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Document) || !System.IO.Path.IsPathFullyQualified(Path))
            throw new ArgumentException("Explicit project name and absolute path required");
        if (Pid is <= 0)
            throw new ArgumentException("PID must be positive or null");
    }
}

public interface IBridgeTransport
{
    IReadOnlyList<int> EnumeratePids();
    Task<JsonObject> SendAsync(string operation, JsonObject arguments, BridgeTarget target, CancellationToken cancellationToken = default);
}
