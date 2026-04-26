using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using NLog;
using NLog.Web;
using PlaywrightWindows.Mcp;
using PlaywrightWindows.Mcp.Core;

namespace FlaUI.Mcp.Mcp.Http;

/// <summary>
/// MCP Streamable HTTP transport host (D-01, D-03). Boots a Kestrel app that
/// delegates JSON-RPC routing on <c>/mcp</c> to the official
/// <c>ModelContextProtocol.AspNetCore</c> 1.2.0 SDK, with the existing
/// FlaUI <see cref="ITool"/> registry adapted via <see cref="ToolBridge"/> so
/// none of the 11 hand-rolled tools require attribute rewrites.
/// </summary>
/// <remarks>
/// SDK confirmed surface (v1.2.0):
/// <list type="bullet">
///   <item><c>MapMcp(IEndpointRouteBuilder, string pattern)</c> — accepts the
///         <c>"/mcp"</c> string overload directly; no <c>MapGroup</c> fallback needed.</item>
///   <item><c>WithHttpTransport(Action&lt;HttpServerTransportOptions&gt;)</c> — exposes
///         <c>Stateless</c>, <c>EnableLegacySse</c>, <c>IdleTimeout</c>.</item>
///   <item><c>McpServerTool.Create(Delegate, McpServerToolCreateOptions)</c> — used by
///         the bridge per signature in <c>ModelContextProtocol.Core.xml</c>.</item>
/// </list>
/// <c>EnableLegacySse = false</c> guarantees no co-mounted <c>/sse</c> on this branch
/// (D-03 keeps legacy SSE on a separate <c>--transport sse</c> process).
/// </remarks>
public static class HttpTransport
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Public entry point for production startup (Plan 4-04 wires this from
    /// <c>Program.cs</c>). Listens on <paramref name="bindAddress"/>:<paramref name="port"/>
    /// and serves <c>/mcp</c> until <paramref name="cancellationToken"/> trips.
    /// </summary>
    public static Task RunAsync(
        SessionManager sessionManager,
        ElementRegistry elementRegistry,
        ToolRegistry toolRegistry,
        string bindAddress,
        int port,
        CancellationToken cancellationToken)
        => RunCoreAsync(sessionManager, elementRegistry, toolRegistry, bindAddress, port, boundUrl: null, cancellationToken);

    /// <summary>
    /// Test-only overload: yields the actual bound URL via <paramref name="boundUrl"/>
    /// once Kestrel has selected a free port (used when <paramref name="port"/> is 0).
    /// </summary>
    internal static Task RunAsync(
        SessionManager sessionManager,
        ElementRegistry elementRegistry,
        ToolRegistry toolRegistry,
        string bindAddress,
        int port,
        TaskCompletionSource<string> boundUrl,
        CancellationToken cancellationToken)
        => RunCoreAsync(sessionManager, elementRegistry, toolRegistry, bindAddress, port, boundUrl, cancellationToken);

    private static async Task RunCoreAsync(
        SessionManager sessionManager,
        ElementRegistry elementRegistry,
        ToolRegistry toolRegistry,
        string bindAddress,
        int port,
        TaskCompletionSource<string>? boundUrl,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        builder.Host.UseNLog();
        builder.WebHost.UseUrls($"http://{bindAddress}:{port}");

        builder.Services.AddSingleton(sessionManager);
        builder.Services.AddSingleton(elementRegistry);
        builder.Services.AddSingleton(toolRegistry);

        var mcpBuilder = builder.Services
            .AddMcpServer()
            .WithHttpTransport(opts =>
            {
                opts.Stateless = false;
                opts.EnableLegacySse = false;          // D-03: no /sse on http branch
                opts.IdleTimeout = TimeSpan.FromHours(2);
            });

        // Register all 11 hand-rolled tools via the bridge. ToolRegistry is already
        // populated by Program.cs before this entry-point runs.
        foreach (var tool in ToolBridge.CreateAll(toolRegistry))
        {
            mcpBuilder.Services.AddSingleton(tool);
        }

        var app = builder.Build();
        app.UseMiddleware<OriginValidationMiddleware>();
        app.MapMcp("/mcp");

        Log.Info("FlaUI-MCP Streamable HTTP listening on http://{Bind}:{Port}/mcp", bindAddress, port);

        // Surface the actually-bound URL after start (used by tests with port 0).
        if (boundUrl != null)
        {
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                try
                {
                    var server = app.Services.GetRequiredService<IServer>();
                    var addrs = server.Features.Get<IServerAddressesFeature>();
                    var url = addrs?.Addresses.FirstOrDefault() ?? $"http://{bindAddress}:{port}";
                    boundUrl.TrySetResult(url);
                }
                catch (Exception ex)
                {
                    boundUrl.TrySetException(ex);
                }
            });
        }

        await app.RunAsync(cancellationToken);
    }
}
