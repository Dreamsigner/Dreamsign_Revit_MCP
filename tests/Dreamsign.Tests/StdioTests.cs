using System.Text.Json.Nodes;
using Dreamsign.Contracts;
using Dreamsign.Server;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Dreamsign.Tests;

public sealed class StdioTests
{
    [Fact]
    public async Task OfficialClientInitializesListsToolsAndCallsServerOverStdio()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Dreamsign.Tests", Guid.NewGuid().ToString("N"));
        new StateStore(directory).Configure(new("Example", @"C:\Projects\Example.rvt"));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            var executable = Environment.GetEnvironmentVariable("DREAMSIGN_TEST_SERVER_EXE");
            if (executable is not null && !File.Exists(executable)) throw new InvalidOperationException("Published server executable not found");
            var transport = new StdioClientTransport(new()
            {
                Name = "Dreamsign integration test",
                // The SDK's Windows client wraps commands in cmd /c; resolve dotnet via cwd
                // to avoid its quoting bug when the executable path contains spaces.
                Command = executable is null ? "dotnet" : Path.GetFileName(executable),
                WorkingDirectory = Path.GetDirectoryName(executable ?? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")) ?? AppContext.BaseDirectory,
                InheritEnvironmentVariables = false,
                EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables(),
                Arguments = executable is null
                    ? [typeof(Program).Assembly.Location, "serve", "--profile", "inspection", "--data-dir", directory]
                    : ["serve", "--profile", "inspection", "--data-dir", directory]
            });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: deadline.Token);
            var tools = await client.ListToolsAsync(cancellationToken: deadline.Token);
            Assert.Equal(12, tools.Count);
            Assert.All(tools, tool => Assert.True(ToolCatalog.Get(tool.Name).ReadOnly));
            var state = await client.CallToolAsync("get_project_state", cancellationToken: deadline.Token);
            Assert.NotEqual(true, state.IsError);
            var content = Assert.IsType<TextContentBlock>(Assert.Single(state.Content));
            Assert.Equal("Example", JsonNode.Parse(content.Text)!["target"]!["document"]!.ToString());
            var blocked = await client.CallToolAsync("save_project", new Dictionary<string, object?> { ["document_token"] = "unused" }, cancellationToken: deadline.Token);
            Assert.True(blocked.IsError);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
