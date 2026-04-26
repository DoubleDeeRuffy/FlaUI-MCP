using System.Collections.Concurrent;
using System.Text.Json;
using FlaUI.Mcp.Mcp.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;

namespace PlaywrightWindows.Mcp;

/// <summary>
/// MCP SSE Transport — exposes the MCP server over HTTP with Server-Sent Events.
/// GET  /sse      → SSE stream (sends endpoint event, then message events)
/// POST /messages → receives JSON-RPC requests, responses sent back via the SSE stream
/// </summary>
public class SseTransport
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly McpServer _server;
    private readonly string _bindAddress;
    private readonly int _port;
    private readonly TaskCompletionSource<string>? _boundUrl;
    private readonly ConcurrentDictionary<string, SseClient> _clients = new();

    /// <summary>
    /// Backward-compat shim: defaults bind address to <c>127.0.0.1</c> (D-06 loopback default).
    /// </summary>
    public SseTransport(McpServer server, int port)
        : this(server, "127.0.0.1", port)
    {
    }

    /// <summary>
    /// D-06 parity ctor: explicit <paramref name="bindAddress"/> matches the new HTTP transport
    /// surface so <c>--bind</c> can widen the bind from the loopback default.
    /// </summary>
    public SseTransport(McpServer server, string bindAddress, int port)
        : this(server, bindAddress, port, boundUrl: null)
    {
    }

    /// <summary>
    /// Test-only ctor: surfaces the actually-bound URL via <paramref name="boundUrl"/> once
    /// Kestrel has selected a free port (used when <paramref name="port"/> is 0).
    /// </summary>
    internal SseTransport(McpServer server, string bindAddress, int port, TaskCompletionSource<string>? boundUrl)
    {
        _server = server;
        _bindAddress = bindAddress;
        _port = port;
        _boundUrl = boundUrl;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        builder.Host.UseNLog();
        builder.WebHost.UseUrls($"http://{_bindAddress}:{_port}");

        var app = builder.Build();

        // D-06: enforce Origin allowlist BEFORE the legacy SSE endpoints so external
        // Origins never reach the streaming/messaging handlers (DNS-rebinding defense).
        app.UseMiddleware<OriginValidationMiddleware>();

        app.MapGet("/sse", async (HttpContext context) =>
        {
            var sessionId = Guid.NewGuid().ToString("N");
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";

            var client = new SseClient(context.Response);
            _clients[sessionId] = client;

            try
            {
                // Send the endpoint event — tells the client where to POST messages
                var endpointUrl = $"/messages?sessionId={sessionId}";
                await client.SendEventAsync("endpoint", endpointUrl);

                // Keep the connection alive until cancelled
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _clients.TryRemove(sessionId, out _);
            }
        });

        app.MapPost("/messages", async (HttpContext context) =>
        {
            var sessionId = context.Request.Query["sessionId"].ToString();
            if (string.IsNullOrEmpty(sessionId) || !_clients.TryGetValue(sessionId, out var client))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid or missing sessionId");
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Empty request body");
                return;
            }

            JsonRpcRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<JsonRpcRequest>(body, McpProtocol.JsonOptions);
            }
            catch (JsonException ex)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync($"Invalid JSON: {ex.Message}");
                return;
            }

            if (request == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Failed to deserialize request");
                return;
            }

            // Process the request
            var response = await _server.HandleRequestAsync(request);

            if (response != null)
            {
                var responseJson = JsonSerializer.Serialize(response, McpProtocol.JsonOptions);
                await client.SendEventAsync("message", responseJson);
            }

            // Acknowledge the POST
            context.Response.StatusCode = 202;
            await context.Response.WriteAsync("Accepted");
        });

        Logger.Info("FlaUI-MCP SSE server listening on http://{Bind}:{Port}", _bindAddress, _port);
        Logger.Info("  SSE endpoint:     GET  http://{Bind}:{Port}/sse", _bindAddress, _port);
        Logger.Info("  Message endpoint:  POST http://{Bind}:{Port}/messages?sessionId=<id>", _bindAddress, _port);

        // Surface the actually-bound URL after start (used by tests with port 0).
        if (_boundUrl != null)
        {
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                try
                {
                    var server = app.Services.GetRequiredService<IServer>();
                    var addrs = server.Features.Get<IServerAddressesFeature>();
                    var url = addrs?.Addresses.FirstOrDefault() ?? $"http://{_bindAddress}:{_port}";
                    _boundUrl.TrySetResult(url);
                }
                catch (Exception ex)
                {
                    _boundUrl.TrySetException(ex);
                }
            });
        }

        await app.RunAsync(cancellationToken);
    }

    private class SseClient
    {
        private readonly HttpResponse _response;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public SseClient(HttpResponse response)
        {
            _response = response;
        }

        public async Task SendEventAsync(string eventType, string data)
        {
            await _writeLock.WaitAsync();
            try
            {
                await _response.WriteAsync($"event: {eventType}\n");
                await _response.WriteAsync($"data: {data}\n\n");
                await _response.Body.FlushAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}
