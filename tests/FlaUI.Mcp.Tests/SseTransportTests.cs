using Xunit;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// Legacy SSE transport tests. Stubs landed by Wave 0 — Wave 1 (Plan 03) replaces with real assertions.
/// </summary>
public class SseTransportTests
{
    [Fact(Skip = "Wave 1: HTTP-02 — implemented in Plan 03")]
    public void LegacyEndpointsRespond()
    {
        // Wave 1: --transport sse mounts /sse and /messages and they respond to legacy clients.
    }
}
