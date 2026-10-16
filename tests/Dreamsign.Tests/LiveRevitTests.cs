using System.Text.Json.Nodes;
using Dreamsign.Contracts;
using Dreamsign.Server;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Dreamsign.Tests;

public sealed class LiveRevitFactAttribute : FactAttribute
{
    public LiveRevitFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DREAMSIGN_RUN_REVIT_TESTS") != "1")
            Skip = "Opt-in only: requires bridge 0.6 and a dedicated saved Dreamsign_MCP_* project";
    }
}

public sealed class LiveRevitTests
{
    [LiveRevitFact]
    public async Task PublishedMcpServerReadsTheActualTestDocument()
    {
        var data = Environment.GetEnvironmentVariable("DREAMSIGN_REVIT_TEST_DATA_DIR")
            ?? throw new InvalidOperationException("Configure the test data directory");
        var target = new StateStore(data).Target;
        Assert.StartsWith("Dreamsign_MCP_", target.Document);
        var executable = Environment.GetEnvironmentVariable("DREAMSIGN_TEST_SERVER_EXE")
            ?? throw new InvalidOperationException("Set DREAMSIGN_TEST_SERVER_EXE to the published server executable");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var transport = new StdioClientTransport(new()
        {
            Name = "Dreamsign native integration",
            Command = Path.GetFileName(executable), WorkingDirectory = Path.GetDirectoryName(executable),
            Arguments = ["serve", "--profile", "inspection", "--data-dir", data],
            InheritEnvironmentVariables = false, EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables()
        });
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: deadline.Token);
        var response = await client.CallToolAsync("get_active_document", cancellationToken: deadline.Token);
        Assert.NotEqual(true, response.IsError);
        var document = JsonNode.Parse(Assert.IsType<TextContentBlock>(Assert.Single(response.Content)).Text)!;
        Assert.Equal(target.Document, document["document"]!.ToString());
        Assert.Equal(Path.GetFullPath(target.Path), Path.GetFullPath(document["path"]!.ToString()), ignoreCase: true);
        Assert.Equal(ToolCatalog.Version, document["bridge_version"]!.ToString());
        Assert.True(document["ui_available"]!.GetValue<bool>());
        var levels = await client.CallToolAsync("get_levels", cancellationToken: deadline.Token);
        Assert.NotEqual(true, levels.IsError);
        Assert.True(JsonNode.Parse(Assert.IsType<TextContentBlock>(Assert.Single(levels.Content)).Text)!["count"]!.GetValue<int>() > 0);
    }

    [LiveRevitFact]
    public async Task NativePreviewSnapshotInvalidationAndSingleUsePlan()
    {
        var data = Environment.GetEnvironmentVariable("DREAMSIGN_REVIT_TEST_DATA_DIR")
            ?? throw new InvalidOperationException("DREAMSIGN_REVIT_TEST_DATA_DIR must point to a configured test target");
        var store = new StateStore(data);
        Assert.StartsWith("Dreamsign_MCP_", store.Target.Document);
        Assert.StartsWith("Dreamsign_MCP_", Path.GetFileNameWithoutExtension(store.Target.Path));
        var runtime = new BridgeRuntime(store, new NamedPipeTransport(), "full");
        async Task<JsonObject> Call(string op, JsonObject args)
        {
            if (ToolCatalog.Get(op).Schema["properties"]?["response_mode"] is not null) args["response_mode"] = "full";
            return await runtime.HandleAsync(op, args);
        }
        var document = await Call("get_active_document", new());
        Assert.True(document["success"]!.GetValue<bool>(), document.ToJsonString());
        Assert.Equal(ToolCatalog.Version, document["bridge_version"]!.ToString());
        var token = document["document_token"]!.ToString();
        var source = "Dreamsign_MCP_" + Guid.NewGuid().ToString("N");
        string? uid = null;
        try
        {
            var created = await Call("create_elements", new() { ["document_token"] = token,
                ["spec"] = new JsonArray(new JsonObject { ["kind"] = "level", ["source_id"] = source, ["name"] = source, ["elevation_m"] = 321.123 }) });
            Assert.True(created["success"]!.GetValue<bool>(), created.ToJsonString());
            uid = created["results"]![0]!["unique_id"]!.ToString();
            var filter = new JsonObject { ["source_id"] = source, ["fields"] = new JsonArray("unique_id", "name", "elevation_m") };
            var before = await Call("capture_snapshot", new() { ["filter"] = filter.DeepClone() });
            var snapshotId = before["snapshot_id"]!.ToString();
            var plan = await Call("preview_batch", new() { ["document_token"] = token, ["save"] = false,
                ["actions"] = new JsonArray(new JsonObject { ["operation"] = "modify", ["id"] = uid, ["changes"] = new JsonObject { ["name"] = source + "-applied" } }) });
            Assert.Equal("preview", plan["status"]!.ToString());
            Assert.True((await Call("read_snapshot", new() { ["snapshot_id"] = snapshotId }))["success"]!.GetValue<bool>());
            var applied = await Call("apply_plan", new() { ["document_token"] = token, ["plan_id"] = plan["plan_id"]!.ToString() });
            Assert.True(applied["success"]!.GetValue<bool>(), applied.ToJsonString());
            Assert.False((await Call("read_snapshot", new() { ["snapshot_id"] = snapshotId }))["success"]!.GetValue<bool>());
            Assert.False((await Call("apply_plan", new() { ["document_token"] = token, ["plan_id"] = plan["plan_id"]!.ToString() }))["success"]!.GetValue<bool>());
            var after = await Call("capture_snapshot", new() { ["filter"] = filter.DeepClone() });
            var diff = await Call("diff_snapshots", new() { ["before_id"] = snapshotId, ["after_id"] = after["snapshot_id"]!.ToString() });
            Assert.Contains(uid, diff["changed"]!.AsArray().Select(n => n!.ToString()));
        }
        finally
        {
            // Only the exact element created by this test may be removed. Never save automatically.
            if (uid is not null)
            {
                var deleted = await Call("delete_elements", new() { ["document_token"] = token, ["ids"] = new JsonArray(uid) });
                Assert.True(deleted["success"]!.GetValue<bool>(), "Temporary level cleanup failed: " + deleted.ToJsonString());
            }
        }
    }
}
