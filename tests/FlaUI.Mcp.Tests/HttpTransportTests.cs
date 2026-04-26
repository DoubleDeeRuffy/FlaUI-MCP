using System.Net;
using Xunit;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// Streamable HTTP transport tests (HTTP-01, HTTP-03, HTTP-04). Boots the real
/// <c>HttpTransport</c> via the shared fixture and exchanges JSON-RPC over /mcp.
/// </summary>
public class HttpTransportTests : IClassFixture<HttpTransportFixture>
{
    private readonly HttpTransportFixture _fx;

    public HttpTransportTests(HttpTransportFixture fx) => _fx = fx;

    [Fact]
    public async Task MapsMcpEndpoint()
    {
        // HTTP-01: POST /mcp with dual Accept gets a JSON-RPC initialize response and Mcp-Session-Id.
        var (sessionId, json) = await _fx.InitializeMcpAsync();

        Assert.False(string.IsNullOrEmpty(sessionId), "Mcp-Session-Id header must be issued on initialize");
        Assert.Equal("2.0", json.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, json.GetProperty("id").GetInt32());
        Assert.True(json.TryGetProperty("result", out _), "initialize must return a `result` object");
    }

    [Fact]
    public async Task EndToEndToolCall()
    {
        // HTTP-03: round-trip ListWindows over /mcp.
        var (sessionId, _) = await _fx.InitializeMcpAsync();
        await SendInitializedNotificationAsync(sessionId);

        var listBody = """
            {"jsonrpc":"2.0","id":2,"method":"tools/list"}
            """;
        using var listResp = await _fx.PostJsonRpcAsync(sessionId, listBody);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var listJson = HttpTransportFixture.ExtractJsonRpcPayload(await listResp.Content.ReadAsStringAsync());
        var tools = listJson.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();
        Assert.Contains(tools, t => t.GetProperty("name").GetString() == "windows_list_windows");

        var callBody = """
            {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"windows_list_windows","arguments":{}}}
            """;
        using var callResp = await _fx.PostJsonRpcAsync(sessionId, callBody);
        Assert.Equal(HttpStatusCode.OK, callResp.StatusCode);
        var callJson = HttpTransportFixture.ExtractJsonRpcPayload(await callResp.Content.ReadAsStringAsync());
        Assert.False(callJson.TryGetProperty("error", out _),
            $"tools/call ListWindows must not return JSON-RPC error: {callJson.GetRawText()}");
        Assert.True(callJson.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task SessionIdLifecycle()
    {
        // HTTP-04: tools/list without Mcp-Session-Id → 400; with random GUID → 404.
        var body = """
            {"jsonrpc":"2.0","id":1,"method":"tools/list"}
            """;
        using var noSessionResp = await _fx.PostJsonRpcAsync(sessionId: string.Empty, body);
        Assert.Equal(HttpStatusCode.BadRequest, noSessionResp.StatusCode);

        using var unknownResp = await _fx.PostJsonRpcAsync(
            sessionId: Guid.NewGuid().ToString("N"),
            body);
        Assert.Equal(HttpStatusCode.NotFound, unknownResp.StatusCode);
    }

    private async Task SendInitializedNotificationAsync(string sessionId)
    {
        // SDK requires the client to acknowledge initialize before tool calls succeed.
        var body = """
            {"jsonrpc":"2.0","method":"notifications/initialized"}
            """;
        using var resp = await _fx.PostJsonRpcAsync(sessionId, body);
        // Accept any 2xx — SDK semantics differ between sync ack and 202 Accepted.
        Assert.True((int)resp.StatusCode is >= 200 and < 300,
            $"notifications/initialized must succeed: {resp.StatusCode}");
    }
}
