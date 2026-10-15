using System.Text.Json.Nodes;
using Dreamsign.Contracts;
using Xunit;

namespace Dreamsign.Tests;

public class ContractTests
{
    [Fact]
    public void DefaultCatalogRemainsAtLeastFortyPercentBelowOriginalFullCatalog()
    {
        var json = new JsonArray(ToolCatalog.ForProfile("workflow").Select(t => (JsonNode)t.ToJson()).ToArray()).ToJsonString();
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) < 31232 * .6);
        Assert.Equal(22, ToolCatalog.Commands.Count);
    }
    [Theory]
    [InlineData("inspection", 12)]
    [InlineData("workflow", 18)]
    [InlineData("modeling", 20)]
    [InlineData("diagnostic", 13)]
    [InlineData("full", 24)]
    public void ProfilesPreservePublishedCapabilities(string profile, int count)
        => Assert.Equal(count, ToolCatalog.ForProfile(profile).Count);

    [Fact]
    public void InspectionHasNoModelMutations()
        => Assert.All(ToolCatalog.ForProfile("inspection"), t => Assert.True(t.ReadOnly));

    [Theory]
    [InlineData("apply_plan", "{\"plan_id\":\"p\"}")]
    [InlineData("run_workflow", "{\"workflow\":\"remove_tags\",\"category\":\"DoorTags\"}")]
    [InlineData("create_elements", "{\"document_token\":\"t\",\"spec\":[]}")]
    [InlineData("query_elements", "{\"filter\":{\"limit\":10001}}")]
    [InlineData("query_elements", "{\"filter\":{\"fields\":[\"password\"]}}")]
    [InlineData("query_elements", "{\"filter\":{\"predicates\":[{\"field\":\"height_m\",\"op\":\"exec\",\"value\":1}]}}")]
    [InlineData("modify_elements", "{\"document_token\":\"t\",\"ids\":[1],\"changes\":{}}")]
    public void UnsafeArgumentsAreRejected(string name, string json)
        => Assert.Throws<ArgumentException>(() => SchemaValidator.Validate(JsonNode.Parse(json), ToolCatalog.Get(name).Schema));

    [Fact]
    public void TypedFilterAndWindowModificationAreAccepted()
    {
        SchemaValidator.Validate(JsonNode.Parse("""{"filter":{"active_view":true,"family":"Window","box_min_m":[0,0,0],"box_max_m":[10,10,10],"predicates":[{"field":"height_m","op":"gt","value":2}]}}"""), ToolCatalog.Get("query_elements").Schema);
        SchemaValidator.Validate(JsonNode.Parse("""{"document_token":"t","ids":[1],"changes":{"sill_m":0.8}}"""), ToolCatalog.Get("modify_elements").Schema);
    }

    [Fact]
    public void UnknownProfilesAndToolsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => ToolCatalog.ForProfile("unknown"));
        Assert.Throws<ArgumentException>(() => ToolCatalog.Get("execute_arbitrary_code"));
    }
}
