using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace PlaywrightWindows.Mcp;

/// <summary>
/// MCP SSE Transport — exposes the MCP server over HTTP with Server-Sent Events.
/// GET  /sse      → SSE stream (sends endpoint event, then message events)
/// POST /messages → receives JSON-RPC requests, responses sent back via the SSE stream
/// </summary>
public class SseTransport
{
    private readonly McpServer _server;
    private readonly int _port;
    private readonly ConcurrentDictionary<string, SseClient> _clients = new();

    public SseTransport(McpServer server, int port)
    {
        _server = server;
        _port = port;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{_port}");

        var app = builder.Build();

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

        Console.Error.WriteLine($"FlaUI-MCP SSE server listening on http://0.0.0.0:{_port}");
        Console.Error.WriteLine($"  SSE endpoint:     GET  http://localhost:{_port}/sse");
        Console.Error.WriteLine($"  Message endpoint:  POST http://localhost:{_port}/messages?sessionId=<id>");

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
