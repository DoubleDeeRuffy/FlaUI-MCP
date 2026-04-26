using Xunit;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// Origin-header rejection middleware tests. Stubs landed by Wave 0 — Wave 1 (Plan 02) replaces.
/// </summary>
public class OriginMiddlewareTests
{
    [Fact(Skip = "Wave 1: HTTP-07 — implemented in Plan 02")]
    public void RejectsExternalOrigin()
    {
        // Wave 1: assert Origin header rejected (HTTP 403) unless absent, "null", localhost, or 127.0.0.1.
    }
}
