using System.Text.Json.Nodes;
using Dreamsign.Contracts;
using Dreamsign.Server;
using Xunit;

namespace Dreamsign.Tests;

public sealed class RuntimeTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Dreamsign.Tests", Guid.NewGuid().ToString("N"));
    private static readonly BridgeTarget Target = new("ExampleProject", @"C:\Projects\ExampleProject.rvt");
    private StateStore Store()
    {
        var store = new StateStore(directory);
        store.Configure(Target);
        return store;
    }
    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    [Fact]
    public async Task StaleConfiguredPidIsReplacedByDiscoveredPid()
    {
        var fake = new FakeTransport((op, a, t) => Task.FromResult(new JsonObject { ["success"] = t.Pid == 7, ["document"] = Target.Document, ["path"] = Target.Path, ["bridge_version"] = "0.3.0" }), [7]);
        var result = await TargetResolver.ResolveAsync(Target with
        {
            Pid = 99
        }, fake);
        Assert.True(result["success"]!.GetValue<bool>());
        Assert.Equal(7, result["pid"]!.GetValue<int>());
    }

    [Fact]
    public async Task AmbiguityAndWrongPathsNeverExecuteWrites()
    {
        var fake = new FakeTransport((op, a, t) => Task.FromResult(new JsonObject { ["success"] = true, ["document"] = Target.Document, ["path"] = Target.Path, ["bridge_version"] = "0.4.0" }), [7, 8]);
        var runtime = new BridgeRuntime(Store(), fake, "full");
        var result = await runtime.HandleAsync("save_project", new()
        {
            ["document_token"] = "t"
        });
        Assert.Equal("AMBIGUOUS_TARGET", result["code"]!.GetValue<string>());
        Assert.All(fake.Calls, call => Assert.Equal("get_active_document", call.Operation));
        var wrong = new FakeTransport((op, a, t) => Task.FromResult(new JsonObject { ["success"] = true, ["document"] = Target.Document, ["path"] = @"C:\Other\ExampleProject.rvt" }), [7]);
        Assert.Equal("TARGET_UNAVAILABLE", (await TargetResolver.ResolveAsync(Target, wrong))["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task HiddenAndInvalidToolsAreRejectedBeforeDiscovery()
    {
        var fake = new FakeTransport((op, a, t) => throw new Exception("Must not reach transport"), [7]);
        var runtime = new BridgeRuntime(Store(), fake, "inspection");
        await Assert.ThrowsAsync<ArgumentException>(() => runtime.HandleAsync("save_project", new() { ["document_token"] = "t" }));
        await Assert.ThrowsAsync<ArgumentException>(() => runtime.HandleAsync("query_elements", new() { ["filter"] = new JsonObject { ["limit"] = 10000 } }));
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task PreviewIsCompactAndDoesNotMutateMapping()
    {
        var fake = Successful((op, a) => new JsonObject { ["success"] = true, ["status"] = "preview", ["plan_id"] = "p", ["actions"] = new JsonArray(new JsonObject { ["large"] = "data" }), ["before"] = new JsonArray(), ["request_id"] = "../../outside" });
        var store = Store();
        var runtime = new BridgeRuntime(store, fake, "full");
        var result = await runtime.HandleAsync("set_window_sills", new()
        {
            ["document_token"] = "t",
            ["filter"] = new JsonObject(),
            ["value_m"] = 0.8
        });
        Assert.Equal("p", result["plan_id"]!.GetValue<string>());
        Assert.Null(result["actions"]);
        Assert.Null(result["before"]);
        Assert.Empty(store.Read()["mapping"]!.AsObject());
        var report = result["detail_report"]!.GetValue<string>();
        Assert.StartsWith(Path.Combine(directory, "reports") + Path.DirectorySeparatorChar, report);
        Assert.NotNull(JsonNode.Parse(File.ReadAllText(report))!["result"]!["actions"]);
    }

    [Fact]
    public async Task TimeoutIsReturnedWithoutRetry()
    {
        var fake = Successful((op, a) => ToolCatalog.Error("TIMEOUT_OUTCOME_UNKNOWN", "Inspect before retry"));
        var result = await new BridgeRuntime(Store(), fake, "full").HandleAsync("save_project", new()
        {
            ["document_token"] = "t"
        });
        Assert.Equal("TIMEOUT_OUTCOME_UNKNOWN", result["code"]!.GetValue<string>());
        Assert.Single(fake.Calls, c => c.Operation == "save_project");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task SaveFailureReportsWhetherChangesWereApplied(int created, bool expected)
    {
        var fake = Successful((op, a) => new JsonObject { ["success"] = false, ["code"] = "SAVE_FAILED", ["created"] = created });
        var result = await new BridgeRuntime(Store(), fake, "full").HandleAsync("apply_plan", new()
        {
            ["document_token"] = "t",
            ["plan_id"] = "p"
        });
        Assert.Equal(expected, result["changes_applied"]!.GetValue<bool>());
    }

    [Fact]
    public async Task OlderBridgeCannotReceiveFeatureCalls()
    {
        var fake = new FakeTransport((op, a, t) => Task.FromResult(new JsonObject { ["success"] = true, ["document"] = Target.Document, ["path"] = Target.Path, ["bridge_version"] = "0.2.0" }), [7]);
        var result = await new BridgeRuntime(Store(), fake, "full").HandleAsync("capture_snapshot", new()
        {
            ["filter"] = new JsonObject()
        });
        Assert.Equal("UPGRADE_REQUIRED", result["code"]!.GetValue<string>());
        Assert.All(fake.Calls, c => Assert.Equal("get_active_document", c.Operation));
    }

    private static FakeTransport Successful(Func<string, JsonObject, JsonObject> result) => new((op, a, t) => Task.FromResult(op == "get_active_document"
        ? new JsonObject { ["success"] = true, ["document"] = Target.Document, ["path"] = Target.Path, ["bridge_version"] = "0.4.0" }
        : result(op, a)), [7]);
}

internal sealed class FakeTransport(Func<string, JsonObject, BridgeTarget, Task<JsonObject>> send, IReadOnlyList<int> pids) : IBridgeTransport
{
    public List<(string Operation, JsonObject Arguments, BridgeTarget Target)> Calls { get; } = [];
    public IReadOnlyList<int> EnumeratePids() => pids;
    public Task<JsonObject> SendAsync(string operation, JsonObject arguments, BridgeTarget target, CancellationToken cancellationToken = default)
    {
        Calls.Add((operation, (JsonObject)arguments.DeepClone(), target));
        return send(operation, arguments, target);
    }
}
