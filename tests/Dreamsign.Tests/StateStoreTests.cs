using Dreamsign.Contracts;
using System.Text.Json.Nodes;
using Dreamsign.Server;
using Xunit;

namespace Dreamsign.Tests;

public sealed class StateStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Dreamsign.Tests", Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    [Fact]
    public void ChangingPidPreservesMappingButChangingModelBacksUpAndResetsIt()
    {
        var store = new StateStore(directory);
        var target = new Dreamsign.Contracts.BridgeTarget("Example", @"C:\Example.rvt", 1);
        store.Configure(target);
        var state = store.Read();
        state["mapping"] = new JsonObject { ["wall-1"] = new JsonObject { ["unique_id"] = "uid" } };
        store.Save(state);
        store.Configure(target with
        {
            Pid = 2
        });
        Assert.Single(store.Read()["mapping"]!.AsObject());
        store.Configure(target with
        {
            Path = @"C:\Other\Example.rvt"
        });
        Assert.Empty(store.Read()["mapping"]!.AsObject());
        var backup = Assert.Single(Directory.GetFiles(directory, "project-state.before-*.json"));
        Assert.Single(JsonNode.Parse(File.ReadAllText(backup))!["mapping"]!.AsObject());
    }

    [Fact]
    public void ASecondProcessLeaseIsRefusedUntilRelease()
    {
        var store = new StateStore(directory);
        using (store.AcquireLease())
            Assert.Throws<IOException>(() => store.AcquireLease());
        using var next = store.AcquireLease();
        Assert.True(next.CanWrite);
    }
}
