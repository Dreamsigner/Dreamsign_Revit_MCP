using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using Dreamsign.Contracts;
using Dreamsign.Server;
using Xunit;

namespace Dreamsign.Tests;

public sealed class TransportTests
{
    [Theory]
    [InlineData("valid", null)]
    [InlineData("wrong_id", "PROTOCOL_ERROR")]
    [InlineData("null", "PROTOCOL_ERROR")]
    [InlineData("eof", "INCOMPLETE_RESPONSE")]
    [InlineData("oversize", "RESPONSE_TOO_LARGE")]
    [InlineData("timeout", "TIMEOUT_OUTCOME_UNKNOWN")]
    public async Task PipeProtocolChecksIdentitySizeEofAndDeadline(string mode, string? expected)
    {
        if (!OperatingSystem.IsWindows()) return;
        var pid = Random.Shared.Next(1500000000, 1900000000);
        using var pipe = new NamedPipeServerStream(ToolCatalog.PipePrefix + pid, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Assert.Contains(pid, new NamedPipeTransport().EnumeratePids());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = Task.Run(async () =>
        {
            await pipe.WaitForConnectionAsync(deadline.Token);
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            var request = JsonNode.Parse((await reader.ReadLineAsync(deadline.Token))!)!;
            Assert.Equal("get_active_document", request["operation"]!.ToString());
            if (mode == "timeout") { await Task.Delay(700, deadline.Token); return; }
            if (mode == "eof") { pipe.Disconnect(); return; }
            var response = mode == "null" ? "null\n" : mode == "oversize" ? new string(' ', ToolCatalog.ResponseBytes + 1) + "\n"
                : new JsonObject { ["success"] = true, ["request_id"] = mode == "wrong_id" ? "other" : request["request_id"]!.ToString() }.ToJsonString() + "\n";
            try { await pipe.WriteAsync(Encoding.UTF8.GetBytes(response), deadline.Token); }
            catch (IOException) when (mode == "oversize") { } // Client closes after the limit.
        });
        var result = await new NamedPipeTransport(TimeSpan.FromMilliseconds(mode == "timeout" ? 400 : 5000))
            .SendAsync("get_active_document", new(), new("Example", @"C:\Example.rvt", pid), deadline.Token);
        await server;
        if (expected is null) Assert.True(result["success"]!.GetValue<bool>());
        else Assert.Equal(expected, result["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task OversizedRequestIsRejectedBeforeConnecting()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new NamedPipeTransport().SendAsync("query_elements",
            new JsonObject { ["oversized"] = new string('x', ToolCatalog.RequestBytes) }, new("Example", @"C:\Example.rvt", 1800000000)));
    }
}
