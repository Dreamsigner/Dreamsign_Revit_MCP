using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dreamsign.Contracts;

namespace Dreamsign.Server;

public sealed class NamedPipeTransport(TimeSpan? responseTimeout = null) : IBridgeTransport
{
    public IReadOnlyList<int> EnumeratePids()
    {
        if (!OperatingSystem.IsWindows()) return [];
        try
        {
            return Directory.EnumerateFiles(@"\\.\pipe\").Select(Path.GetFileName)
                .Where(n => n is not null && n.StartsWith(ToolCatalog.PipePrefix, StringComparison.Ordinal))
                .Select(n => int.TryParse(n![ToolCatalog.PipePrefix.Length..], out var pid) ? pid : 0).Where(p => p > 0).ToArray();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
    public async Task<JsonObject> SendAsync(string operation, JsonObject arguments, BridgeTarget target, CancellationToken cancellationToken = default)
    {
        if (target.Pid is not > 0) throw new ArgumentException("Explicit PID required for pipe transport");
        var id = Guid.NewGuid().ToString("D");
        var request = new JsonObject { ["operation"] = operation, ["arguments"] = arguments.DeepClone(), ["request_id"] = id, ["expected_document"] = target.Document, ["expected_document_path"] = target.Path };
        var bytes = Encoding.UTF8.GetBytes(request.ToJsonString() + "\n");
        if (bytes.Length > ToolCatalog.RequestBytes) throw new ArgumentException("Request exceeds 256 KiB");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(responseTimeout ?? TimeSpan.FromSeconds(35));
        using var pipe = new NamedPipeClientStream(".", ToolCatalog.PipePrefix + target.Pid, PipeDirection.InOut, PipeOptions.Asynchronous);
        var writingStarted = false;
        JsonObject Error(string code, string message)
        {
            var error = ToolCatalog.Error(code, message); error["request_id"] = id; return error;
        }
        try
        {
            await pipe.ConnectAsync(1500, deadline.Token);
            writingStarted = true;
            await pipe.WriteAsync(bytes, deadline.Token);
            await pipe.FlushAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            while (true)
            {
                var count = await pipe.ReadAsync(chunk, deadline.Token);
                if (count == 0) return Error("INCOMPLETE_RESPONSE", "Read state before retrying a write");
                var end = Array.IndexOf(chunk, (byte)10, 0, count);
                var length = end >= 0 ? end : count;
                if (buffer.Length + length > ToolCatalog.ResponseBytes) return Error("RESPONSE_TOO_LARGE", "Read state before retrying a write");
                buffer.Write(chunk, 0, length);
                if (end < 0) continue;
                var response = JsonNode.Parse(new UTF8Encoding(false, true).GetString(buffer.ToArray())) as JsonObject
                    ?? throw new JsonException("Object response required");
                if (response["request_id"]?.ToString() != id) return Error("PROTOCOL_ERROR", "Request identity mismatch; inspect state before retrying a write");
                return response;
            }
        }
        catch (Exception e) when (e is OperationCanceledException or TimeoutException or IOException or UnauthorizedAccessException)
        {
            return writingStarted ? Error("TIMEOUT_OUTCOME_UNKNOWN", "Outcome unknown; read Revit state before retrying a write")
                : Error("BRIDGE_UNAVAILABLE", "Revit 2027 bridge not reachable: " + e.Message);
        }
        catch (Exception e) when (e is JsonException or DecoderFallbackException or InvalidOperationException)
        {
            return Error("PROTOCOL_ERROR", "Invalid bridge response; inspect state before retrying a write");
        }
    }
}
