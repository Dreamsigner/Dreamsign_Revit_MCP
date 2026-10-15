using Dreamsign.Contracts;
using System.Text.Json.Nodes;
using Tomlyn;
using Xunit;

namespace Dreamsign.Tests;

public sealed class ClientIntegrationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Dreamsign_MCP_" + Guid.NewGuid().ToString("N"));
    private string Exe { get { Directory.CreateDirectory(directory); var path=Path.Combine(directory,"Dreamsign.Revit.Mcp.exe"); File.WriteAllText(path,"fixture"); return path; } }
    public void Dispose() { if(Directory.Exists(directory)) Directory.Delete(directory,true); }

    [Fact]
    public void CodexKeepsOtherServersAndBacksUpOriginal()
    {
        var path=Path.Combine(directory,"config.toml"); var exe=Exe;
        const string original="# keep this comment\nmodel = \"example\"\n[mcp_servers.other]\ncommand = \"other\"\n[mcp_servers.astra_revit]\ncommand = 'C:\\Old\\Dreamsign.Revit.Mcp.exe'\nargs = [\n 'serve',\n]\n";
        File.WriteAllText(path, original);
        var result=ClientIntegration.Configure(DesktopClient.Codex,path,exe,directory,"modeling");
        Assert.Equal(original,File.ReadAllText(result.Backup!));
        var parsed=Toml.ToModel(File.ReadAllText(path));
        var servers=(Tomlyn.Model.TomlTable)parsed["mcp_servers"];
        Assert.Equal("other",((Tomlyn.Model.TomlTable)servers["other"])["command"]);
        Assert.Equal(exe,((Tomlyn.Model.TomlTable)servers["astra_revit"])["command"]);
        Assert.Contains("# keep this comment",File.ReadAllText(path));
        Assert.True(ClientIntegration.Remove(DesktopClient.Codex,path,exe,directory));
        Assert.Contains("[mcp_servers.other]",File.ReadAllText(path));
    }

    [Theory]
    [InlineData(DesktopClient.ClaudeDesktop)]
    [InlineData(DesktopClient.Cursor)]
    public void JsonClientsKeepUnknownSettingsAndDoNotOverwriteForeignEntries(DesktopClient client)
    {
        var exe=Exe; var path=Path.Combine(directory,"client.json");
        File.WriteAllText(path,"{\"theme\":\"dark\",\"mcpServers\":{\"other\":{\"command\":\"keep\"}}}");
        ClientIntegration.Configure(client,path,exe,directory,"inspection");
        var parsed=JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal("dark",parsed["theme"]!.ToString());
        Assert.Equal("keep",parsed["mcpServers"]!["other"]!["command"]!.ToString());
        Assert.True(ClientIntegration.Remove(client,path,exe,directory));
        File.WriteAllText(path,"{\"mcpServers\":{\"dreamsign_revit\":{\"command\":\"foreign.exe\"}}}");
        var before=File.ReadAllText(path);
        Assert.Throws<InvalidOperationException>(()=>ClientIntegration.Configure(client,path,exe,directory,"full"));
        Assert.Equal(before,File.ReadAllText(path));
    }

    [Fact]
    public void InvalidTomlIsNeverReplaced()
    {
        var exe=Exe; var path=Path.Combine(directory,"broken.toml");
        File.WriteAllText(path,"model = [broken");
        Assert.ThrowsAny<Exception>(()=>ClientIntegration.Configure(DesktopClient.Codex,path,exe,directory,"workflow"));
        Assert.Equal("model = [broken",File.ReadAllText(path));
    }

    [Fact]
    public void UnsavedOrFamilyProjectCannotBeSelected()
    {
        var store=new StateStore(directory);
        Assert.Throws<ArgumentException>(()=>store.SelectProject("Example","",1,false));
        Assert.Throws<ArgumentException>(()=>store.SelectProject("Family",Path.Combine(directory,"Family.rfa"),1,true));
        Assert.False(File.Exists(store.StatePath));
    }
}
