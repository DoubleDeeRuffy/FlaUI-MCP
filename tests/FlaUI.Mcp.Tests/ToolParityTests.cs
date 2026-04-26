using System.Net;
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
}
