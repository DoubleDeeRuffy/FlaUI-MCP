using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using NLog;
using Skoosoft.Windows.Manager;
using FlaUI.Mcp.Logging;
using PlaywrightWindows.Mcp;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Tools;

// === Constants ===
const string ServiceName = "FlaUI-MCP";
const string FirewallRuleName = "FlaUI-MCP";
const string TaskName = "FlaUI-MCP";

// === 1. Parse command-line arguments (extracted to CliOptions for unit-testability) ===
var opts = FlaUI.Mcp.CliOptions.Parse(args);
var silent = opts.Silent;
var debug = opts.Debug;
var install = opts.Install;
var uninstall = opts.Uninstall;
var console = opts.Console;
var task = opts.Task;
var removeTask = opts.RemoveTask;
var transport = opts.Transport;
var port = opts.Port;
var helpRequested = opts.Help;

// === 1b. Re-attach to parent console under WinExe so Console.* writes are visible (TSK-04) ===
if (console || install || uninstall || task || removeTask || helpRequested)
{
    NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
    // Return value intentionally ignored: false means no parent console (e.g. launched
    // by Task Scheduler) — Console.WriteLine then becomes a silent no-op, which is fine.
}

if (helpRequested)
{
    Console.WriteLine("FlaUI-MCP — MCP server for Windows desktop automation");
    Console.WriteLine();
    Console.WriteLine("Usage: FlaUI.Mcp.exe [options]");
    Console.WriteLine();
    Console.WriteLine("Registration:");
    Console.WriteLine("  --task              Register as scheduled task (runs at user logon, sees desktop)");
    Console.WriteLine("  --removetask        Remove scheduled task");
    Console.WriteLine();
    Console.WriteLine("Runtime:");
    Console.WriteLine("  --console, -c       Run in console mode (attach to parent shell, enable ConsoleTarget)");
    Console.WriteLine("  --debug, -d         Enable debug-level logging (Debug.log)");
    Console.WriteLine("  --silent, -s        Suppress prompts during registration");
    Console.WriteLine("  --transport <type>  Transport: http (default), sse, or stdio");
    Console.WriteLine("  --bind <addr>       Kestrel bind address (default: 127.0.0.1; use 0.0.0.0 for LAN)");
    Console.WriteLine("  --port <number>     Listen port (default: 3020)");
    Console.WriteLine("  --help, -?          Show this help");
    Console.WriteLine();
    Console.WriteLine("Aliases (compatibility with v0.x service-based scripts):");
    Console.WriteLine("  --install, -i       Same as --task");
    Console.WriteLine("  --uninstall, -u     Same as --removetask");
    Environment.Exit(0);
}

// === 1c. Debugger guard: F5 from VS auto-enables -c -d, kills stale FlaUI.Mcp procs (TSK-05) ===
if (Debugger.IsAttached)
{
    console = true;
    debug = true;

    var currentPid = Environment.ProcessId;
    foreach (var stale in Process.GetProcessesByName("FlaUI.Mcp")
                                 .Where(p => p.Id != currentPid))
    {
        try
        {
            stale.Kill();
            stale.WaitForExit(5000);
        }
        catch
        {
            // Process may have exited between enumeration and kill — race is fine.
        }
    }
}

// === Register CodePages encoding provider (required for netsh/FirewallManager on German Windows) ===
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// === 2. Console window sizing (SVC-11, TSK-08) ===
if (console)
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
LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport != "stdio");

// === 5. Get logger ===
Logger? logger = LogManager.GetCurrentClassLogger();
logger.Info("FlaUI-MCP starting (transport={Transport}, bind={Bind}, port={Port}, debug={Debug})", transport, opts.BindAddress, port, debug);

// === 6. Unhandled exception handler (SVC-09) ===
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    logger?.Error(e.ExceptionObject as Exception, "Unhandled exception");
};

// === 7. Firewall rule (SVC-07) — for SSE and HTTP transports ===
var exeFilePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
if (transport == "sse" || transport == "http")
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

// === 9. Register / unregister scheduled task (TSK-02, TSK-03; aliases per D-2) ===
// -install/-i alias --task; -uninstall/-u alias --removetask
if (task || install)
{
    // D-1 auto-migration: silently uninstall any legacy FlaUI-MCP Windows Service
    // before creating the scheduled task. Idempotent — no-op if absent.
    if (ServiceManager.DoesServiceExist(ServiceName))
    {
        logger?.Info("Detected legacy FlaUI-MCP Windows Service — uninstalling before creating scheduled task");
        try
        {
            ServiceManager.Uninstall(ServiceName, silent: true);
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "Failed to auto-uninstall legacy service during --task migration");
            Console.WriteLine($"WARNING: Failed to auto-uninstall legacy service: {ex.Message}");
            Environment.Exit(1);
        }
    }

    // Defensive idempotency: delete pre-existing task with same name
    try { WinTaskSchedulerManager.Delete(TaskName); } catch { /* idempotent — ignore */ }

    try
    {
        WinTaskSchedulerManager.CreateOnLogon(
            name: TaskName,
            description: "Starts FlaUI-MCP at user logon (runs in user desktop session)",
            execFilePath: exeFilePath,
            execArguments: "");
        Console.WriteLine($"Scheduled task '{TaskName}' registered. Will start at next user logon.");
        logger?.Info("Scheduled task '{Task}' registered", TaskName);
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        logger?.Error(ex, "Failed to create scheduled task");
        Console.WriteLine($"Failed to create scheduled task: {ex.Message}");
        Environment.Exit(1);
    }
}

if (removeTask || uninstall)
{
    // D-1 reverse rule: do NOT touch the service. The service should be gone
    // already from a prior --task call, or never existed. Only remove the task.
    try
    {
        WinTaskSchedulerManager.Delete(TaskName);  // idempotent
        Console.WriteLine($"Scheduled task '{TaskName}' removed.");
        logger?.Info("Scheduled task '{Task}' removed", TaskName);
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        logger?.Error(ex, "Failed to remove scheduled task");
        Console.WriteLine($"Failed to remove scheduled task: {ex.Message}");
        Environment.Exit(1);
    }
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
    if (transport == "http")
    {
        await FlaUI.Mcp.Mcp.Http.HttpTransport.RunAsync(
            sessionManager, elementRegistry, toolRegistry,
            opts.BindAddress, port, cts.Token);
    }
    else if (transport == "sse")
    {
        var sseTransport = new SseTransport(server, opts.BindAddress, port);
        await sseTransport.RunAsync(cts.Token);
    }
    else if (transport == "stdio")
    {
        await server.RunAsync(cts.Token);
    }
    else
    {
        logger?.Error("Unknown transport '{Transport}' — must be one of: http, sse, stdio", transport);
        Environment.Exit(2);
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

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(int dwProcessId);
    internal const int ATTACH_PARENT_PROCESS = -1;
}
