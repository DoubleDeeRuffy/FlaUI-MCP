namespace FlaUI.Mcp.Tests;

/// <summary>
/// Test trait categories. Tests requiring real Windows windows or a live MCP client
/// (e.g. Claude Code in <c>"type":"http"</c> mode) carry
/// <c>[Trait("Category", TestCategories.Manual)]</c> and are excluded from the default
/// CI run via <c>--filter "Category!=Manual"</c>.
/// </summary>
public static class TestCategories
{
    /// <summary>Live-client / live-Windows tests requiring manual smoke.</summary>
    public const string Manual = "Manual";
}
