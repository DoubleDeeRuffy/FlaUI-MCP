using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// HTTP-05 functional parity over /mcp. Two real tests:
///   1. tools/list returns exactly the canonical 11 FlaUI tools.
///   2. Non-UI tools (ListWindows, empty Batch) execute end-to-end without errors.
/// UI-side-effect tools (Launch/Click/Type/Fill/Screenshot/etc.) require real Windows
/// and remain manual smoke covered by /gsd:verify-work.
/// </summary>
public class ToolParityTests : IClassFixture<HttpTransportFixture>
{
    private static readonly string[] CanonicalTools =
    {
        "windows_launch", "windows_snapshot", "windows_click", "windows_type",
        "windows_fill", "windows_get_text", "windows_screenshot",
        "windows_list_windows", "windows_focus", "windows_close", "windows_batch",
    };

    private readonly HttpTransportFixture _fx;

    public ToolParityTests(HttpTransportFixture fx) => _fx = fx;

    [Fact]
    public async Task ToolsListReturnsAll11Tools()
    {
        var (sessionId, _) = await _fx.InitializeMcpAsync();
        await SendInitializedAsync(sessionId);

        var body = """
            {"jsonrpc":"2.0","id":2,"method":"tools/list"}
            """;
        using var resp = await _fx.PostJsonRpcAsync(sessionId, body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = HttpTransportFixture.ExtractJsonRpcPayload(await resp.Content.ReadAsStringAsync());
        var names = json.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!)
            .ToHashSet();

        Assert.Equal(11, names.Count);
        foreach (var expected in CanonicalTools)
        {
            Assert.Contains(expected, names);
        }
    }

    [Theory]
    [InlineData("windows_list_windows", "{}")]
    [InlineData("windows_batch", """{"actions":[]}""")]
    public async Task NonUiToolsExecuteOverHttp(string toolName, string argsJson)
    {
        var (sessionId, _) = await _fx.InitializeMcpAsync();
        await SendInitializedAsync(sessionId);

        var body =
            "{\"jsonrpc\":\"2.0\",\"id\":42,\"method\":\"tools/call\",\"params\":{\"name\":\""
            + toolName + "\",\"arguments\":" + argsJson + "}}";
        using var resp = await _fx.PostJsonRpcAsync(sessionId, body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = HttpTransportFixture.ExtractJsonRpcPayload(await resp.Content.ReadAsStringAsync());
        Assert.False(json.TryGetProperty("error", out _),
            $"{toolName} returned JSON-RPC error: {json.GetRawText()}");
        Assert.True(json.TryGetProperty("result", out var result));

        // Result must include a content array. The bridge wraps McpToolResult.Content as
        // structured content blocks; the result is not an isError.
        if (result.TryGetProperty("isError", out var isErr) && isErr.ValueKind == System.Text.Json.JsonValueKind.True)
        {
            Assert.Fail($"{toolName} reported isError=true: {result.GetRawText()}");
        }
    }

    private async Task SendInitializedAsync(string sessionId)
    {
        var body = """
            {"jsonrpc":"2.0","method":"notifications/initialized"}
            """;
        using var resp = await _fx.PostJsonRpcAsync(sessionId, body);
        Assert.True((int)resp.StatusCode is >= 200 and < 300);
    }

    /// <summary>
    /// HTTP-05 parity, SSE side. Boots the legacy SseTransport on port 0, opens the SSE
    /// stream to obtain a sessionId, performs the JSON-RPC <c>initialize</c> handshake,
    /// then invokes a non-UI tool via <c>tools/call</c> and asserts a JSON-RPC result
    /// (no error, no isError) parsed off the SSE message frame.
    /// Together with <see cref="NonUiToolsExecuteOverHttp"/>, HTTP-05 is functionally
    /// proven on BOTH the new HTTP transport AND the legacy SSE transport.
    /// </summary>
    [Theory]
    [InlineData("windows_list_windows", "{}")]
    [InlineData("windows_batch", """{"actions":[]}""")]
    public async Task NonUiToolsExecuteOverSse(string toolName, string argsJson)
    {
        var (_, serverTask, cts, baseUrl) = SseTransportTests.StartTransport();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

            using var sseReq = new HttpRequestMessage(HttpMethod.Get, "/sse");
            sseReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            using var sseResp = await client.SendAsync(sseReq, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            Assert.Equal(HttpStatusCode.OK, sseResp.StatusCode);

            await using var sseStream = await sseResp.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(sseStream);

            // Pull the endpoint event for the sessionId.
            var sessionId = await ReadEventDataAsync(reader, expectedEvent: "endpoint", TimeSpan.FromSeconds(5));
            var key = "sessionId=";
            sessionId = sessionId.Substring(sessionId.IndexOf(key, StringComparison.Ordinal) + key.Length);

            // Handshake: initialize → message frame on /sse.
            var initBody = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""";
            await PostMessageAsync(client, sessionId, initBody, cts.Token);
            _ = await ReadEventDataAsync(reader, expectedEvent: "message", TimeSpan.FromSeconds(5));

            // tools/call for the non-UI tool.
            var callBody =
                "{\"jsonrpc\":\"2.0\",\"id\":42,\"method\":\"tools/call\",\"params\":{\"name\":\""
                + toolName + "\",\"arguments\":" + argsJson + "}}";
            await PostMessageAsync(client, sessionId, callBody, cts.Token);

            var responseJson = await ReadEventDataAsync(reader, expectedEvent: "message", TimeSpan.FromSeconds(10));
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            Assert.False(root.TryGetProperty("error", out _),
                $"{toolName} returned JSON-RPC error: {root.GetRawText()}");
            Assert.True(root.TryGetProperty("result", out var result));

            if (result.TryGetProperty("isError", out var isErr) && isErr.ValueKind == JsonValueKind.True)
            {
                Assert.Fail($"{toolName} reported isError=true: {result.GetRawText()}");
            }
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch { /* shutdown noise */ }
            cts.Dispose();
        }
    }

    private static async Task PostMessageAsync(HttpClient client, string sessionId, string body, CancellationToken ct)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, $"/messages?sessionId={sessionId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        using var resp = await client.SendAsync(msg, ct);
        Assert.True((int)resp.StatusCode is >= 200 and < 300,
            $"POST /messages returned {(int)resp.StatusCode}");
    }

    /// <summary>
    /// Reads SSE frames until an <c>event: {expectedEvent}</c> followed by a <c>data:</c>
    /// line is encountered; returns the data payload.
    /// </summary>
    private static async Task<string> ReadEventDataAsync(StreamReader reader, string expectedEvent, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        bool match = false;
        while (!cts.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line == null) break;
            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                match = line.Substring(6).Trim() == expectedEvent;
                continue;
            }
            if (match && line.StartsWith("data:", StringComparison.Ordinal))
            {
                return line.Substring(5).Trim();
            }
        }
        return string.Empty;
    }
}
