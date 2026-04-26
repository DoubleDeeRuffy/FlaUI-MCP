using Xunit;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// Tool-parity tests across http and sse transports. Stub landed by Wave 0 — Wave 1 (Plan 02) replaces.
/// </summary>
public class ToolParityTests
{
    [Theory(Skip = "Wave 1: HTTP-05 — implemented in Plan 02")]
    [InlineData("Launch")]
    [InlineData("Snapshot")]
    [InlineData("Click")]
    [InlineData("Type")]
    [InlineData("Fill")]
    [InlineData("GetText")]
    [InlineData("Screenshot")]
    [InlineData("ListWindows")]
    [InlineData("FocusWindow")]
    [InlineData("CloseWindow")]
    [InlineData("Batch")]
    public void AllToolsCallableOverHttp(string toolName)
    {
        // Wave 1: assert each tool is callable on both --transport http and --transport sse.
        _ = toolName;
    }
}
