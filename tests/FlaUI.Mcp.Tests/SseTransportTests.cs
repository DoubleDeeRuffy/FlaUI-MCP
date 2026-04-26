using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using PlaywrightWindows.Mcp;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// HTTP-02 regression: legacy <c>--transport sse</c> still mounts <c>/sse</c> + <c>/messages</c>.
/// Includes the SSE-side branch of the D-06 Origin allowlist parity (HTTP-07) as a sub-step.
/// Boots the real SseTransport on a free Kestrel port via the internal test ctor that
/// surfaces the bound URL — same pattern as HttpTransportFixture.
/// </summary>
public class SseTransportTests
{
    private static ToolRegistry BuildRegistry()
    {
        var sessionManager = new SessionManager();
        var elementRegistry = new ElementRegistry();
        var registry = new ToolRegistry();
        registry.RegisterTool(new LaunchTool(sessionManager));
        registry.RegisterTool(new SnapshotTool(sessionManager, elementRegistry));
        registry.RegisterTool(new ClickTool(elementRegistry));
        registry.RegisterTool(new TypeTool(elementRegistry));
        registry.RegisterTool(new FillTool(elementRegistry));
        registry.RegisterTool(new GetTextTool(elementRegistry));
        registry.RegisterTool(new ScreenshotTool(sessionManager, elementRegistry));
        registry.RegisterTool(new ListWindowsTool(sessionManager));
        registry.RegisterTool(new FocusWindowTool(sessionManager));
        registry.RegisterTool(new CloseWindowTool(sessionManager));
        registry.RegisterTool(new BatchTool(sessionManager, elementRegistry));
        return registry;
    }

    internal static (SseTransport transport, Task serverTask, CancellationTokenSource cts, string baseUrl)
        StartTransport()
    {
        var registry = BuildRegistry();
        var server = new McpServer(registry);
        var bound = new TaskCompletionSource<string>();
        var cts = new CancellationTokenSource();

        // Reach the internal 4-arg ctor (server, bindAddress, port, boundUrl).
        var ctor = typeof(SseTransport).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            new[] { typeof(McpServer), typeof(string), typeof(int), typeof(TaskCompletionSource<string>) },
            modifiers: null)!;
        var transport = (SseTransport)ctor.Invoke(new object?[] { server, "127.0.0.1", 0, bound });

        var serverTask = transport.RunAsync(cts.Token);
        var baseUrl = bound.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
        return (transport, serverTask, cts, baseUrl);
    }

    [Fact]
    public async Task LegacyEndpointsRespond()
    {
        var (_, serverTask, cts, baseUrl) = StartTransport();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

            // 1. GET /sse with Accept: text/event-stream returns 200 + event-stream content type.
            using (var req = new HttpRequestMessage(HttpMethod.Get, "/sse"))
            {
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
                Assert.Equal("text/event-stream", resp.Content.Headers.ContentType?.MediaType);

                // Pull the endpoint event so we know /messages session is registered.
                var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new StreamReader(stream);
                var sessionId = await ReadEndpointSessionAsync(reader, TimeSpan.FromSeconds(5));
                Assert.False(string.IsNullOrEmpty(sessionId));

                // 2. POST /messages with a JSON-RPC initialize body returns 2xx (legacy 202).
                using var msg = new HttpRequestMessage(HttpMethod.Post, $"/messages?sessionId={sessionId}")
                {
                    Content = new StringContent(
                        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""",
                        Encoding.UTF8,
                        "application/json"),
                };
                using var msgResp = await client.SendAsync(msg, cts.Token);
                Assert.True((int)msgResp.StatusCode is >= 200 and < 300,
                    $"POST /messages returned {(int)msgResp.StatusCode}");
            }

            // 3. SSE-side D-06 Origin parity: a request from evil.example.com is rejected with 403.
            using (var req = new HttpRequestMessage(HttpMethod.Get, "/sse"))
            {
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                req.Headers.Add("Origin", "https://evil.example.com");
                using var resp = await client.SendAsync(req, cts.Token);
                Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            }
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch { /* shutdown noise */ }
            cts.Dispose();
        }
    }

    /// <summary>
    /// Reads SSE frames until an <c>event: endpoint</c> line is followed by a <c>data:</c>
    /// line containing <c>?sessionId=...</c>. Returns the parsed session id.
    /// </summary>
    private static async Task<string> ReadEndpointSessionAsync(StreamReader reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        bool sawEndpointEvent = false;
        while (!cts.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line == null) break;
            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                sawEndpointEvent = line.Contains("endpoint", StringComparison.Ordinal);
                continue;
            }
            if (sawEndpointEvent && line.StartsWith("data:", StringComparison.Ordinal))
            {
                var payload = line.Substring(5).Trim();
                var key = "sessionId=";
                var idx = payload.IndexOf(key, StringComparison.Ordinal);
                if (idx >= 0) return payload.Substring(idx + key.Length);
            }
        }
        return string.Empty;
    }
}
