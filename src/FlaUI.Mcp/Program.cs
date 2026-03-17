using System.Diagnostics;
using System.ServiceProcess;
using NLog;
using Skoosoft.ServiceHelperLib;
using Skoosoft.Windows.Manager;
using FlaUI.Mcp.Logging;
using PlaywrightWindows.Mcp;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Tools;

// === Constants ===
const string ServiceName = "FlaUI-MCP";
const string FirewallRuleName = "FlaUI-MCP";

// === 1. Parse command-line arguments — boolean flags via joined parameter string ===
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

// === 2. Console window sizing (SVC-11) ===
if (!Debugger.IsAttached && Environment.UserInteractive)
{
    try
    {
        Console.BufferWidth = 180;
        Console.WindowWidth = 180;
        Console.WindowHeight = 50;
    }
    catch
    {
        // Ignore - redirected output or Windows Terminal
    }
}

// === 3. CleanOldLogfiles() ===
var logDirectory = LoggingConfig.LogDirectory;
LogArchiver.CleanOldLogfiles(logDirectory);

// === 4. ConfigureLogging(debug) ===
LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport == "sse");

// === 5. Get logger ===
Logger? logger = LogManager.GetCurrentClassLogger();
logger.Info("FlaUI-MCP starting (transport={transport}, debug={debug})", transport, debug);

// === 6. Unhandled exception handler (SVC-09) ===
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    logger?.Error(e.ExceptionObject as Exception, "Unhandled exception");
};

// === 7. Firewall rule (SVC-07) — only for SSE transport ===
var exeFilePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
if (transport == "sse")
{
    if (!FirewallManager.CheckRule(FirewallRuleName))
        FirewallManager.SetRule(FirewallRuleName, exeFilePath);
}

// === 8. Stop running service before console mode (SVC-08) ===
if (Environment.UserInteractive)
{
    if (ServiceManager.DoesServiceExist(ServiceName))
    {
        try
        {
            var service = new ServiceController(ServiceName);
            if (service.Status == ServiceControllerStatus.Running)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "Failed to stop running service");
        }
    }
}

// === 9. Install or Uninstall service (SVC-01, SVC-02, SVC-03, SVC-06) ===
if (install)
{
    ServiceManager.Install(ServiceName, exeFilePath, silent);
    // Firewall rule also created during install (if SSE)
    if (!FirewallManager.CheckRule(FirewallRuleName))
        FirewallManager.SetRule(FirewallRuleName, exeFilePath);
    Environment.Exit(0);
}

if (uninstall)
{
    ServiceManager.Uninstall(ServiceName, silent);
    Environment.Exit(0);
}

// === 10. Create shared services and run ===
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
catch (Exception ex)
{
    logger?.Error(ex, "Main exception");
    throw;
}
finally
{
    LogManager.Shutdown();
    sessionManager.Dispose();
}
