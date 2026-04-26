using Xunit;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// Streamable HTTP transport tests. Stubs landed by Wave 0 — Wave 1 (Plan 02) replaces
/// the skips with real assertions backed by Microsoft.AspNetCore.Mvc.Testing.
/// </summary>
public class HttpTransportTests
{
    [Fact(Skip = "Wave 1: HTTP-01 — implemented in Plan 02")]
    public void MapsMcpEndpoint()
    {
        // Wave 1: assert WebApplication maps POST/GET /mcp via ModelContextProtocol.AspNetCore SDK.
    }

    [Fact(Skip = "Wave 1: HTTP-03 — implemented in Plan 02")]
    [Trait("Category", TestCategories.Manual)]
    public void EndToEndToolCall()
    {
        // Wave 1: live tool round-trip with real Claude Code in "type":"http" mode.
    }

    [Fact(Skip = "Wave 1: HTTP-04 — implemented in Plan 02")]
    public void SessionIdLifecycle()
    {
        // Wave 1: Mcp-Session-Id auto-issue on initialize; 400 if absent on subsequent;
        // 404 if expired.
    }
}
