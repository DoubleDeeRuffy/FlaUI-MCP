using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
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

// === 1. Parse command-line arguments ===
var silent = false;
var debug = false;
var install = false;
var uninstall = false;
var console = false;
var task = false;
var removeTask = false;
var transport = "sse";
var port = 8080;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "--install" or "-i":
            install = true;
            break;
        case "--uninstall" or "-u":
            uninstall = true;
            break;
        case "--silent" or "-s":
            silent = true;
            break;
        case "--debug" or "-d":
            debug = true;
            break;
        case "--console" or "-c":
            console = true;
            break;
        case "--task":
            task = true;
            break;
        case "--removetask":
            removeTask = true;
            break;
        case "--transport" when i + 1 < args.Length:
            transport = args[++i].ToLowerInvariant();
            break;
        case "--port" when i + 1 < args.Length:
            if (int.TryParse(args[++i], out var p)) port = p;
            break;
    }
}

// === Register CodePages encoding provider (required for netsh/FirewallManager on German Windows) ===
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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
    try
    {
        if (!FirewallManager.CheckRule(FirewallRuleName))
            FirewallManager.SetRule(FirewallRuleName, exeFilePath);
    }
    catch (Exception ex)
    {
        logger?.Error(ex, "Failed to configure firewall rule");
    }
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

// === 9b. Scheduled Task registration (runs in user session, can see desktop windows) ===
const string TaskName = "FlaUI-MCP";
if (task)
{
    // Create a scheduled task that runs at logon in the user session
    var taskExeArgs = debug ? "--debug" : "";
    var taskCommand = string.IsNullOrEmpty(taskExeArgs)
        ? $"\"{exeFilePath}\""
        : $"\"{exeFilePath}\" {taskExeArgs}";

    var schtasksArgs = $"/create /tn \"{TaskName}\" /tr \"{taskCommand}\" /sc onlogon /rl highest /f";
    logger?.Info("Creating scheduled task: schtasks {Args}", schtasksArgs);
    Console.WriteLine($"Creating scheduled task '{TaskName}'...");

    var process = Process.Start(new ProcessStartInfo
    {
        FileName = "schtasks.exe",
        Arguments = schtasksArgs,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    });
    process?.WaitForExit(30000);
    var output = process?.StandardOutput.ReadToEnd()?.Trim();
    var error = process?.StandardError.ReadToEnd()?.Trim();

    if (process?.ExitCode == 0)
    {
        Console.WriteLine($"Scheduled task '{TaskName}' created. It will start FlaUI-MCP at user logon.");
        Console.WriteLine("The task runs in the user session, so FlaUI can see desktop windows.");
        logger?.Info("Scheduled task created successfully");
    }
    else
    {
        Console.WriteLine($"Failed to create scheduled task: {error ?? output}");
        logger?.Error("Failed to create scheduled task: {Error}", error ?? output);
    }

    Environment.Exit(process?.ExitCode ?? 1);
}

if (removeTask)
{
    Console.WriteLine($"Removing scheduled task '{TaskName}'...");
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = "schtasks.exe",
        Arguments = $"/delete /tn \"{TaskName}\" /f",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    });
    process?.WaitForExit(30000);
    var output = process?.StandardOutput.ReadToEnd()?.Trim();
    var error = process?.StandardError.ReadToEnd()?.Trim();

    if (process?.ExitCode == 0)
    {
        Console.WriteLine($"Scheduled task '{TaskName}' removed.");
        logger?.Info("Scheduled task removed successfully");
    }
    else
    {
        Console.WriteLine($"Failed to remove scheduled task: {error ?? output}");
        logger?.Error("Failed to remove scheduled task: {Error}", error ?? output);
    }

    Environment.Exit(process?.ExitCode ?? 1);
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
