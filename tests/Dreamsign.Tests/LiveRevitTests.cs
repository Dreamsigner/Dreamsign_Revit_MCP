using System.Text.Json.Nodes;
using Dreamsign.Contracts;
using Dreamsign.Server;
using Xunit;

namespace Dreamsign.Tests;

public sealed class LiveRevitFactAttribute : FactAttribute
{
    public LiveRevitFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DREAMSIGN_RUN_REVIT_TESTS") != "1")
            Skip = "Opt-in only: requires bridge 0.4 and a dedicated saved AstraBridge-Test-* project";
    }
}

public sealed class LiveRevitTests
{
    [LiveRevitFact]
    public async Task NativePreviewSnapshotInvalidationAndSingleUsePlan()
    {
        var data = Environment.GetEnvironmentVariable("DREAMSIGN_REVIT_TEST_DATA_DIR")
            ?? throw new InvalidOperationException("DREAMSIGN_REVIT_TEST_DATA_DIR must point to a configured test target");
        var store = new StateStore(data);
        Assert.StartsWith("AstraBridge-Test-", store.Target.Document);
        Assert.StartsWith("AstraBridge-Test-", Path.GetFileNameWithoutExtension(store.Target.Path));
        var runtime = new BridgeRuntime(store, new NamedPipeTransport(), "full");
        async Task<JsonObject> Call(string op, JsonObject args)
        {
            if (ToolCatalog.Get(op).Schema["properties"]?["response_mode"] is not null) args["response_mode"] = "full";
            return await runtime.HandleAsync(op, args);
        }
        var document = await Call("get_active_document", new());
        Assert.True(document["success"]!.GetValue<bool>(), document.ToJsonString());
        Assert.Equal("0.4.0", document["bridge_version"]!.ToString());
        var token = document["document_token"]!.ToString();
        var source = "migration-test-" + Guid.NewGuid().ToString("N");
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
