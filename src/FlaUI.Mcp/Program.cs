using NLog;
using FlaUI.Mcp.Logging;
using PlaywrightWindows.Mcp;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Tools;

// Parse command-line arguments — boolean flags via joined parameter string
var parameter = string.Join(" ", args).ToLower();
var silent = parameter.Contains("-silent") || parameter.Contains("-s");
var debug = parameter.Contains("-debug") || parameter.Contains("-d");
var install = parameter.Contains("-install") || parameter.Contains("-i");
var uninstall = parameter.Contains("-uninstall") || parameter.Contains("-u");
var console = parameter.Contains("-console") || parameter.Contains("-c");

// Parse value arguments via for-loop
var transport = "sse";
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

// Logging startup sequence
var logDirectory = LoggingConfig.LogDirectory;
LogArchiver.CleanOldLogfiles(logDirectory);
LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport == "sse");
var logger = LogManager.GetCurrentClassLogger();
logger.Info("FlaUI-MCP starting (transport={transport}, debug={debug})", transport, debug);

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
    LogManager.Shutdown();
    sessionManager.Dispose();
}
