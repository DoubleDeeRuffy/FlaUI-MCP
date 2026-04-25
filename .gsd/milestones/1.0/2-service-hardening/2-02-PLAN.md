---
phase: 2-service-hardening
plan: 02
type: execute
wave: 2
depends_on: ["2-01"]
files_modified:
  - src/FlaUI.Mcp/Program.cs
autonomous: true
requirements:
  - SVC-01
  - SVC-02
  - SVC-03
  - SVC-06
  - SVC-07
  - SVC-08
  - SVC-09
  - SVC-10
  - SVC-11

must_haves:
  truths:
    - "Running -install registers a Windows Service named FlaUI-MCP and creates a firewall rule, then exits"
    - "Running -uninstall removes the Windows Service, then exits"
    - "Running -silent suppresses prompts during install/uninstall"
    - "Running -console stops any running FlaUI-MCP service first, then starts the server"
    - "An unhandled exception is logged to Error.log before the process terminates"
    - "Console window is sized to readable dimensions when running interactively"
    - "Startup executes in order: CleanOldLogfiles, ConfigureLogging, Firewall, StopRunning, Install/Uninstall, Run"
  artifacts:
    - path: "src/FlaUI.Mcp/Program.cs"
      provides: "Complete service lifecycle with startup sequence"
      contains: "ServiceManager.Install"
    - path: "src/FlaUI.Mcp/Program.cs"
      provides: "Firewall rule creation"
      contains: "FirewallManager"
    - path: "src/FlaUI.Mcp/Program.cs"
      provides: "Unhandled exception handler"
      contains: "AppDomain.CurrentDomain.UnhandledException"
  key_links:
    - from: "src/FlaUI.Mcp/Program.cs"
      to: "Skoosoft.ServiceHelperLib.ServiceManager"
      via: "Install/Uninstall calls"
      pattern: "ServiceManager\\.(Install|Uninstall)"
    - from: "src/FlaUI.Mcp/Program.cs"
      to: "Skoosoft.Windows.Manager.FirewallManager"
      via: "CheckRule/SetRule calls"
      pattern: "FirewallManager\\.(CheckRule|SetRule)"
    - from: "src/FlaUI.Mcp/Program.cs"
      to: "Environment.Exit"
      via: "Exit after install/uninstall"
      pattern: "Environment\\.Exit\\(0\\)"
    - from: "src/FlaUI.Mcp/Program.cs"
      to: "ServiceController"
      via: "Stop running service"
      pattern: "ServiceController.*Stop"
---

<objective>
Implement the complete Windows Service lifecycle in Program.cs: startup sequence, firewall rule, service install/uninstall, stop-before-console, crash handler, and console sizing.

Purpose: This is the core of Phase 2 — it transforms the server from a simple console app into a production-ready Windows Service with full CLI control and crash safety.
Output: Program.cs restructured with the complete startup sequence per convention.
</objective>

<execution_context>
@C:/Users/uhgde/.claude/get-shit-done/workflows/execute-plan.md
@C:/Users/uhgde/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/PROJECT.md
@.gsd/ROADMAP.md
@.gsd/phases/2-service-hardening/2-CONTEXT.md
@.gsd/phases/2-service-hardening/2-01-SUMMARY.md

<interfaces>
<!-- From windows-service-conventions.md — the canonical startup sequence: -->
<!--
1. Parse command-line parameters
2. Set console window size (if not service, not debugger)
3. CleanOldLogfiles()
4. ConfigureLogging(debug)
5. _logger = GetCurrentClassLogger()
6. AppDomain.UnhandledException handler
7. Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
8. SetFirewallRuleIfNotExisting(exeFilePath)
9. StopRunningService()
10. InstallOrUninstallService()
11. Build and run WebApplication
-->

<!-- ServiceManager API (from Skoosoft.ServiceHelperLib): -->
```csharp
ServiceManager.Install(string serviceName, string pathToExe, bool silent, bool delayedAutoStart = true);
ServiceManager.Uninstall(string serviceName, bool silent);
ServiceManager.DoesServiceExist(string serviceName);
```

<!-- FirewallManager API (from Skoosoft.Windows.Manager): -->
```csharp
FirewallManager.CheckRule(string name);  // returns bool
FirewallManager.SetRule(string name, string pathToExe);  // creates inbound allow rule
```

<!-- Console window sizing pattern: -->
```csharp
if (!Debugger.IsAttached && !WindowsServiceHelpers.IsWindowsService())
{
    try { Console.BufferWidth = 180; Console.WindowWidth = 180; Console.WindowHeight = 50; }
    catch { /* ignore - redirected output or Windows Terminal */ }
}
```

<!-- StopRunningService pattern: -->
```csharp
if (!WindowsServiceHelpers.IsWindowsService())
{
    if (ServiceManager.DoesServiceExist(ServiceName))
    {
        var service = new ServiceController(ServiceName);
        if (service.Status == ServiceControllerStatus.Running)
            service.Stop();
        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
    }
}
```

<!-- Unhandled exception handler: -->
```csharp
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    _logger?.Error(e.ExceptionObject as Exception, "");
};
```

<!-- Service name and description from CONTEXT.md: -->
<!-- Service name: "FlaUI-MCP" -->
<!-- Display name: "FlaUI-MCP" -->
<!-- Description: "MCP server for Windows desktop automation" -->

<!-- From CONTEXT.md decisions: -->
<!-- Firewall rule opens the configured SSE port -->
<!-- Stdio transport does not need a firewall rule -->
<!-- Rule created during install and when running in console mode -->
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Restructure Program.cs with complete startup sequence</name>
  <files>src/FlaUI.Mcp/Program.cs</files>
  <read_first>
    - src/FlaUI.Mcp/Program.cs
    - C:\Users\uhgde\.claude\knowledge\windows-service-conventions.md
    - C:\Users\uhgde\.claude\knowledge\nlog-conventions.md
    - .gsd/phases/2-service-hardening/2-CONTEXT.md
  </read_first>
  <action>
Restructure Program.cs to follow the canonical startup sequence. The file keeps top-level statements (no explicit Main method). Add required usings at the top:

```csharp
using System.Diagnostics;
using System.ServiceProcess;
using NLog;
using Skoosoft.ServiceHelperLib;
using Skoosoft.Windows.Manager;
```

Note: `using NLog;` and `using Microsoft.Extensions.Hosting;` are needed. The NLog calls (CleanOldLogfiles, ConfigureLogging, LogManager) will be available from Phase 1's implementation. Since Phase 1 may not be executed yet, use placeholder method calls that will be wired when Phase 1 runs. If Phase 1 has already run, use the actual methods.

**The complete restructured Program.cs must follow this exact sequence:**

```
// === Usings ===

// === Constants ===
const string ServiceName = "FlaUI-MCP";
const string FirewallRuleName = "FlaUI-MCP";

// === 1. Parse command-line parameters ===
// (Already done in Plan 01 — boolean flags + value args)

// === 2. Console window sizing (SVC-11) ===
if (!Debugger.IsAttached && !WindowsServiceHelpers.IsWindowsService())
{
    try
    {
        Console.BufferWidth = 180;
        Console.WindowWidth = 180;
        Console.WindowHeight = 50;
    }
    catch
    {
        // Ignore — redirected output or Windows Terminal
    }
}

// === 3. CleanOldLogfiles() ===
// Phase 1 provides this — call it here
// CleanOldLogfiles();

// === 4. ConfigureLogging(debug) ===
// Phase 1 provides this — call it here
// ConfigureLogging(debug);

// === 5. Get logger ===
// var logger = LogManager.GetCurrentClassLogger();
// (Use logger variable for crash handler logging)

// === 6. Unhandled exception handler (SVC-09) ===
Logger? logger = null; // Will be assigned after ConfigureLogging in Phase 1
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    logger?.Error(e.ExceptionObject as Exception, "Unhandled exception");
};

// === 7. Firewall rule (SVC-07) ===
// Only for SSE transport (not stdio)
var exeFilePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
if (transport == "sse")
{
    if (!FirewallManager.CheckRule(FirewallRuleName))
        FirewallManager.SetRule(FirewallRuleName, exeFilePath);
}

// === 8. Stop running service before console mode (SVC-08) ===
if (!WindowsServiceHelpers.IsWindowsService())
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
// (Existing code: sessionManager, elementRegistry, toolRegistry, server, cts, try/finally)
```

Important implementation details:
- Keep all existing code from `var sessionManager = new SessionManager();` through the end of the file INTACT inside the try/finally block
- The `finally` block should keep `sessionManager.Dispose()` and will later get `LogManager.Shutdown()` from Phase 1
- The `try` block wraps the transport selection and run — add a `catch (Exception ex)` that logs: `logger?.Error(ex, "Main exception"); throw;`
- `WindowsServiceHelpers.IsWindowsService()` comes from `Microsoft.Extensions.Hosting.WindowsServices` — add PackageReference `Microsoft.Extensions.Hosting.WindowsServices` to .csproj if not already present (it may already be available via FrameworkReference Microsoft.AspNetCore.App)
- If `WindowsServiceHelpers` is not available, use `!Environment.UserInteractive` as the equivalent check
- The `ServiceController` class requires `using System.ServiceProcess;` — this is available in net8.0-windows

**Phase 1 integration points (commented placeholders):**
Where CleanOldLogfiles() and ConfigureLogging(debug) will go, add comments:
```csharp
// TODO: Phase 1 — CleanOldLogfiles();
// TODO: Phase 1 — ConfigureLogging(debug);
// TODO: Phase 1 — logger = LogManager.GetCurrentClassLogger();
```

These will be replaced when Phase 1 executes. The logger variable is declared before the AppDomain handler so the crash handler can use it once Phase 1 wires it.
  </action>
  <verify>
    <automated>cd "C:\Users\uhgde\source\repos\FlaUI-MCP\src\FlaUI.Mcp" && dotnet build --no-restore 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - Program.cs contains `const string ServiceName = "FlaUI-MCP";`
    - Program.cs contains `AppDomain.CurrentDomain.UnhandledException +=`
    - Program.cs contains `FirewallManager.CheckRule(FirewallRuleName)`
    - Program.cs contains `FirewallManager.SetRule(FirewallRuleName, exeFilePath)`
    - Program.cs contains `ServiceManager.DoesServiceExist(ServiceName)`
    - Program.cs contains `service.Stop()` and `service.WaitForStatus`
    - Program.cs contains `ServiceManager.Install(ServiceName, exeFilePath, silent)`
    - Program.cs contains `ServiceManager.Uninstall(ServiceName, silent)`
    - Program.cs contains `Environment.Exit(0)` after both install and uninstall blocks
    - Program.cs contains `Console.BufferWidth = 180` (console sizing)
    - Program.cs contains `!Debugger.IsAttached` check for console sizing
    - Program.cs contains `logger?.Error(ex,` in the catch block
    - Program.cs contains `sessionManager.Dispose()` in the finally block (preserved from original)
    - The startup sequence order is: console sizing, then TODO CleanOldLogfiles, then TODO ConfigureLogging, then UnhandledException handler, then Firewall, then StopRunningService, then Install/Uninstall, then Run
    - `dotnet build` compiles without errors
  </acceptance_criteria>
  <done>Program.cs has complete startup sequence per convention: console sizing, logging placeholders, crash handler, firewall rule, stop service, install/uninstall with exit, and preserved server run logic</done>
</task>

<task type="auto">
  <name>Task 2: Verify compilation and validate startup sequence order</name>
  <files>src/FlaUI.Mcp/Program.cs</files>
  <read_first>
    - src/FlaUI.Mcp/Program.cs
  </read_first>
  <action>
Run `dotnet build` to verify the full project compiles. If there are any compilation errors:
- Fix missing usings
- Fix type resolution issues (e.g., if WindowsServiceHelpers is not found, use `!Environment.UserInteractive` instead)
- Ensure ServiceController is available (System.ServiceProcess namespace, available in net8.0-windows)

If `Microsoft.Extensions.Hosting.WindowsServices` is needed for `WindowsServiceHelpers`, add the NuGet package:
```xml
<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.*" />
```

After successful compilation, validate the startup sequence order by reading Program.cs top-to-bottom and confirming:
1. CLI parsing is first
2. Console sizing is second
3. CleanOldLogfiles placeholder is third
4. ConfigureLogging placeholder is fourth
5. Logger assignment placeholder is fifth
6. AppDomain.UnhandledException handler is sixth
7. Firewall rule is seventh
8. StopRunningService is eighth
9. Install/Uninstall with Environment.Exit(0) is ninth
10. Server creation and run is tenth (last)

If the order is wrong, reorder the code blocks.
  </action>
  <verify>
    <automated>cd "C:\Users\uhgde\source\repos\FlaUI-MCP\src\FlaUI.Mcp" && dotnet build 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - `dotnet build` exits with "Build succeeded" and 0 errors
    - Program.cs line containing `Console.BufferWidth` appears BEFORE line containing `AppDomain.CurrentDomain.UnhandledException`
    - Program.cs line containing `UnhandledException` appears BEFORE line containing `FirewallManager`
    - Program.cs line containing `FirewallManager` appears BEFORE line containing `StopRunningService` or `ServiceManager.DoesServiceExist`
    - Program.cs line containing `ServiceManager.DoesServiceExist` appears BEFORE line containing `ServiceManager.Install`
    - Program.cs line containing `Environment.Exit(0)` appears BEFORE line containing `new SessionManager()`
  </acceptance_criteria>
  <done>Project compiles cleanly and startup sequence follows the canonical order from windows-service-conventions.md</done>
</task>

</tasks>

<verification>
- `dotnet build` succeeds with no errors
- Program.cs has all 9 service requirements implemented
- Startup sequence follows the canonical order
- Install and uninstall both call Environment.Exit(0)
- Firewall rule only created for SSE transport
- Service stop only attempted when not running as service
- Console sizing only when interactive (not service, not debugger)
</verification>

<success_criteria>
- SVC-01: -install registers Windows Service via ServiceManager.Install
- SVC-02: -uninstall removes service via ServiceManager.Uninstall
- SVC-03: -silent flag passed to Install/Uninstall calls
- SVC-06: Environment.Exit(0) after install or uninstall
- SVC-07: FirewallManager.SetRule creates rule for SSE port
- SVC-08: ServiceController.Stop() called before console mode
- SVC-09: AppDomain.UnhandledException logs before crash
- SVC-10: Startup sequence in correct order
- SVC-11: Console.BufferWidth/WindowWidth/WindowHeight set when interactive
</success_criteria>

<output>
After completion, create `.gsd/phases/2-service-hardening/2-02-SUMMARY.md`
</output>
