using PlaywrightWindows.Mcp;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Tools;

// Parse command-line arguments
var transport = "stdio";
var port = 8080;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--transport" when i + 1 < args.Length:
            transport = args[++i].ToLowerInvariant();
            break;
        case "--port" when i + 1 < args.Length:
            if (int.TryParse(args[++i], out var p)) port = p;
            break;
    }
}

// Create shared services
var sessionManager = new SessionManager();
var elementRegistry = new ElementRegistry();

// Register all tools
var toolRegistry = new ToolRegistry();
toolRegistry.RegisterTool(new LaunchTool(sessionManager));
toolRegistry.RegisterTool(new SnapshotTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new ClickTool(elementRegistry));
toolRegistry.RegisterTool(new TypeTool(elementRegistry));
toolRegistry.RegisterTool(new FillTool(elementRegistry));
toolRegistry.RegisterTool(new GetTextTool(elementRegistry));
toolRegistry.RegisterTool(new ScreenshotTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new ListWindowsTool(sessionManager));
toolRegistry.RegisterTool(new FocusWindowTool(sessionManager));
toolRegistry.RegisterTool(new CloseWindowTool(sessionManager));
toolRegistry.RegisterTool(new BatchTool(sessionManager, elementRegistry));

// Create MCP server (transport-agnostic request handler)
var server = new McpServer(toolRegistry);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    if (transport == "sse")
    {
        var sseTransport = new SseTransport(server, port);
        await sseTransport.RunAsync(cts.Token);
    }
    else
    {
        await server.RunAsync(cts.Token);
    }
}
finally
{
    sessionManager.Dispose();
}
