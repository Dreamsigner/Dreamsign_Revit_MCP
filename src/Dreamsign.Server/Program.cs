using System.Text.Json;
using System.Text.Json.Nodes;
using Dreamsign.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Dreamsign.Server;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = CommandOptions.Parse(args);
            if (options.Command == "help") { Console.WriteLine(Help); return 0; }
            var store = new StateStore(options.DataDirectory);
            if (options.Command != "serve") return await Commands.RunAsync(options, store);
            var runtime = new BridgeRuntime(store, new NamedPipeTransport(), options.Profile);
            var tools = ToolCatalog.ForProfile(options.Profile).Select(t => JsonSerializer.Deserialize<Tool>(t.ToJson().ToJsonString())!).ToArray();
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Services.AddMcpServer(o => o.ServerInfo = new Implementation { Name = "dreamsign-revit-mcp", Version = ToolCatalog.Version })
                .WithStdioServerTransport()
                .WithListToolsHandler((request, ct) => ValueTask.FromResult(new ListToolsResult { Tools = tools }))
                .WithCallToolHandler(async (request, ct) =>
                {
                    JsonObject result;
                    try
                    {
                        var parameters = request.Params ?? throw new ArgumentException("Tool parameters required");
                        var arguments = parameters.Arguments is null ? new JsonObject() : JsonSerializer.SerializeToNode(parameters.Arguments)!.AsObject();
                        result = await runtime.HandleAsync(parameters.Name, arguments, ct);
                    }
                    catch (ArgumentException e) { result = ToolCatalog.Error("INVALID_ARGUMENTS", e.Message); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException) { result = ToolCatalog.Error("STATE_ERROR", e.Message); }
                    return new CallToolResult
                    {
                        Content = [new TextContentBlock { Text = result.ToJsonString() }],
                        IsError = result["success"]?.GetValue<bool>() == false
                    };
                });
            using var host = builder.Build();
            await host.RunAsync();
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or JsonException or System.Xml.XmlException)
        {
            Console.Error.WriteLine(e.Message);
            return 2;
        }
    }

    public const string Help = """
        Dreamsign Revit MCP 0.5.0 — C# / .NET 10 / Revit 2027
        serve       [--profile inspection|workflow|modeling|diagnostic|full] [--data-dir DIR]
        configure   --document TITLE --path ABSOLUTE_RVT [--pid PID] [--data-dir DIR]
        connect     --client Codex|ClaudeDesktop|Cursor [--profile PROFILE] [--data-dir DIR]
        doctor      [--data-dir DIR]                 Read-only target/bridge check
        install     [--plugin-dir DIR] [--replace-existing] [--data-dir DIR]
        uninstall   [--data-dir DIR]                 Remove only this manifest
        catalog     [--profile PROFILE]              Print tools without Revit
        --help

        Default command: serve. Default profile: ASTRA_MCP_PROFILE or workflow.
        Data: --data-dir, DREAMSIGN_MCP_DATA_DIR, or LOCALAPPDATA/DreamsignRevitMCP.
        install never closes or restarts Revit. Keep the distribution in a stable folder.
        """;
}
