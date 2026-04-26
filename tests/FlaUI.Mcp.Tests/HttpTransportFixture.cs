using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FlaUI.Mcp.Mcp.Http;
using PlaywrightWindows.Mcp;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// Boots <see cref="HttpTransport"/> on a free port for the lifetime of an xunit
/// class fixture. Provides a configured <see cref="HttpClient"/> that already has
/// the dual <c>Accept</c> header required by the Streamable-HTTP spec
/// (<c>application/json, text/event-stream</c>) and exposes the actually-bound URL.
/// </summary>
public sealed class HttpTransportFixture : IAsyncLifetime
{
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public string BaseUrl { get; private set; } = string.Empty;
    public HttpClient Client { get; private set; } = new();
    public ToolRegistry ToolRegistry { get; } = new();

    public async Task InitializeAsync()
    {
        var sessionManager = new SessionManager();
        var elementRegistry = new ElementRegistry();

        // Register the same 11 tools that Program.cs wires up.
        ToolRegistry.RegisterTool(new LaunchTool(sessionManager));
        ToolRegistry.RegisterTool(new SnapshotTool(sessionManager, elementRegistry));
        ToolRegistry.RegisterTool(new ClickTool(elementRegistry));
        ToolRegistry.RegisterTool(new TypeTool(elementRegistry));
        ToolRegistry.RegisterTool(new FillTool(elementRegistry));
        ToolRegistry.RegisterTool(new GetTextTool(elementRegistry));
        ToolRegistry.RegisterTool(new ScreenshotTool(sessionManager, elementRegistry));
        ToolRegistry.RegisterTool(new ListWindowsTool(sessionManager));
        ToolRegistry.RegisterTool(new FocusWindowTool(sessionManager));
        ToolRegistry.RegisterTool(new CloseWindowTool(sessionManager));
        ToolRegistry.RegisterTool(new BatchTool(sessionManager, elementRegistry));

        _cts = new CancellationTokenSource();
        var bound = new TaskCompletionSource<string>();

        // Reach the internal test overload via reflection — keeps the public surface
        // exactly the 6-arg signature documented in the plan.
        var run = typeof(HttpTransport).GetMethod(
            "RunAsync",
            BindingFlags.NonPublic | BindingFlags.Static,
            new[]
            {
                typeof(SessionManager),
                typeof(ElementRegistry),
                typeof(ToolRegistry),
                typeof(string),
                typeof(int),
                typeof(TaskCompletionSource<string>),
                typeof(CancellationToken),
            })!;

        _serverTask = (Task)run.Invoke(null, new object[]
        {
            sessionManager,
            elementRegistry,
            ToolRegistry,
            "127.0.0.1",
            0,
            bound,
            _cts.Token,
        })!;

        BaseUrl = await bound.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Client.BaseAddress = new Uri(BaseUrl);
        Client.DefaultRequestHeaders.Accept.Clear();
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_cts != null)
        {
            _cts.Cancel();
            try { if (_serverTask != null) await _serverTask; } catch { /* shutdown noise */ }
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Sends an MCP <c>initialize</c> request and returns the issued <c>Mcp-Session-Id</c>.
    /// SSE response bodies are parsed for the embedded <c>data:</c> JSON line.
    /// </summary>
    public async Task<(string sessionId, JsonElement result)> InitializeMcpAsync(CancellationToken ct = default)
    {
        var body = """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"flaui-mcp-tests","version":"1.0"}}}
            """;
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        var resp = await Client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var sessionId = resp.Headers.TryGetValues("Mcp-Session-Id", out var v) ? v.First() : string.Empty;
        var raw = await resp.Content.ReadAsStringAsync(ct);
        var json = ExtractJsonRpcPayload(raw);
        return (sessionId, json);
    }

    public async Task<HttpResponseMessage> PostJsonRpcAsync(
        string sessionId,
        string body,
        CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrEmpty(sessionId))
        {
            req.Headers.Add("Mcp-Session-Id", sessionId);
        }
        return await Client.SendAsync(req, ct);
    }

    public static JsonElement ExtractJsonRpcPayload(string responseBody)
    {
        // SDK may return either application/json directly, or text/event-stream
        // framing with a `data: { ... }` line. Handle both.
        var trimmed = responseBody.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return JsonDocument.Parse(trimmed).RootElement.Clone();
        }

        foreach (var line in responseBody.Split('\n'))
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var payload = line.Substring(5).TrimStart();
                if (!string.IsNullOrEmpty(payload))
                {
                    return JsonDocument.Parse(payload).RootElement.Clone();
                }
            }
        }

        // Fall through — let downstream tests assert.
        return JsonDocument.Parse("{}").RootElement.Clone();
    }
}
