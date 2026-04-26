using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FlaUI.Mcp.Mcp.Http;

/// <summary>
/// D-06 enforcement: rejects HTTP requests whose <c>Origin</c> header points outside
/// the loopback allowlist. The MCP Streamable HTTP spec REQUIRES Origin validation
/// to defend against DNS-rebinding attacks; the SDK does not provide this middleware
/// out-of-the-box, so it is hand-rolled here (small, well-defined, well-tested).
/// </summary>
/// <remarks>
/// Behavior matrix:
/// <list type="bullet">
///   <item>No <c>Origin</c> header → continue (CLI / native MCP clients).</item>
///   <item><c>Origin: null</c> literal → continue (HTML5 sandboxed iframe per spec).</item>
///   <item><c>Origin</c> parses as URI AND host ∈ { localhost, 127.0.0.1 } → continue.</item>
///   <item>Otherwise → 403 Forbidden, body <c>Origin not allowed</c>, warning logged.</item>
/// </list>
/// Host comparison is case-insensitive (RFC 3986 §3.2.2).
/// </remarks>
public sealed class OriginValidationMiddleware
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<OriginValidationMiddleware> _logger;

    public OriginValidationMiddleware(RequestDelegate next, ILogger<OriginValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();

        // Empty / absent → CLI client, allow.
        if (string.IsNullOrEmpty(origin))
        {
            await _next(context);
            return;
        }

        // "null" literal → sandboxed iframe per HTML5 / spec D-06.
        if (string.Equals(origin, "null", StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && AllowedHosts.Contains(uri.Host))
        {
            await _next(context);
            return;
        }

        _logger.LogWarning("Rejected request with disallowed Origin: {Origin}", origin);
        context.Response.StatusCode = 403;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("Origin not allowed");
    }
}
