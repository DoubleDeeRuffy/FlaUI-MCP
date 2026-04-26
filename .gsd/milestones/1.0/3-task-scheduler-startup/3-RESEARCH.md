# Phase 3: Task Scheduler Startup - Research

**Researched:** 2026-04-26
**Domain:** Windows Task Scheduler integration / WinExe subsystem / Console attach P/Invoke
**Confidence:** HIGH

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-1:** `--task` auto-detects and silently uninstalls any pre-existing v0.x `FlaUI-MCP` Windows Service (idempotent, reuses existing Skoosoft service-helper code path). `--removetask` only removes the scheduled task; it must NOT touch the service.
- **D-2:** `-install` / `-i` is repurposed as alias for `--task`; `-uninstall` / `-u` as alias for `--removetask`. Aliases produce identical behavior (same auto-migration, exit codes, logging). Existing scripts continue to work but now register a Task Scheduler task.
- **D-3a:** Trigger scope is **any-user logon** (not pinned to installing user's SID). If Skoosoft API requires explicit user, document and pick sensible default.
- **D-3b:** No logon-to-launch delay. Use whatever `WinTaskSchedulerManager.CreateOnLogon()` provides as default.
- **D-4:** `--help` shows `--task` / `--removetask` as the primary registration method, prominently placed. `-install` / `-uninstall` listed under "Aliases" / "Compatibility" section near the bottom (not promoted, no "deprecated" annotation).

### Claude's Discretion
- Exact `--help` wording and section labels.
- Whether to print one-line confirmation when D-1's auto-uninstall fires (lean toward single info-level NLog entry).
- Internal naming for Task Scheduler wrapper layer.
- Task Scheduler task name — default to `FlaUI-MCP` unless research surfaces a reason to differ.
- Stale-process kill criteria under `Debugger.IsAttached` (TSK-05) — kill by process name `FlaUI.Mcp` excluding own PID is the literal requirement; tightening is planner's call.

### Deferred Ideas (OUT OF SCOPE)
- Per-user task installation (`--task --me-only`) — D-3a chose any-user.
- Configurable logon delay (`--task --delay <seconds>`) — D-3b chose no delay.
- Standalone `migrate-to-task.ps1` fleet rollout tooling.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TSK-01 | `OutputType=WinExe` in .csproj — OS-level console suppression | §Standard Stack (csproj change), §Code Examples (WinExe block) |
| TSK-02 | `--task` uses `WinTaskSchedulerManager.CreateOnLogon()` with InteractiveToken + Highest | §Architecture (Skoosoft pattern), §Code Examples (CreateOnLogon) |
| TSK-03 | `--removetask` uses `WinTaskSchedulerManager.Delete()` (idempotent) | §Architecture, §Code Examples (Delete) |
| TSK-04 | `AttachConsole(ATTACH_PARENT_PROCESS)` before any Console.WriteLine on -c/-i/-u | §Code Examples (AttachConsole P/Invoke) |
| TSK-05 | `Debugger.IsAttached` auto-enables -c -d and kills stale FlaUI.Mcp procs (excl. own PID) | §Code Examples (Debugger guard), §Pitfalls (self-kill) |
| TSK-06 | NLog ConsoleTarget gated behind `-console` flag (not transport == "sse") | §Architecture (NLog gate switch) |
| TSK-07 | Remove `Microsoft.Extensions.Hosting.WindowsServices` package | §Standard Stack (csproj remove) |
| TSK-08 | Console window sizing guarded against headless WinExe mode | §Pitfalls (sizing crash) |
| TSK-09 | `--help` text reflects Task Scheduler as primary registration | §Architecture (help reorg per D-4) |

## Project Constraints (from CLAUDE.md)

- **GSD workflow only** — file edits go through GSD execute-phase, not direct edits.
- **German umlauts UTF-8** — proper ä/ö/ü/ß. Skoosoft canonical example uses German description ("Startet … beim Benutzer-Login"); FlaUI-MCP can use English or German per existing convention (existing log messages in repo are English — keep consistent).
- **LSP first** — use goToDefinition/findReferences for symbol navigation during planning/execution.
- **Memory palace first** — durable findings persisted to `projects/FlaUI-MCP/` wing (see Sources).
- **PowerShell on Windows** — backslash paths, `$env:VAR` form (does not affect this phase's C# code).

## Summary

Phase 3 pivots FlaUI-MCP from a Windows Service (Session 0, no desktop) to a Task Scheduler LogonTrigger task (Session 1, user desktop) so FlaUI/UIA3 can see and automate desktop windows. The transformation has two halves: **(a)** subsystem + console plumbing — flip `OutputType` to `WinExe`, gate console writes behind `AttachConsole(ATTACH_PARENT_PROCESS)`, gate NLog ConsoleTarget behind `-console`, guard window sizing against no-console mode; and **(b)** scheduling — replace the existing raw `schtasks.exe` shell-out (currently in Program.cs lines 165–235) with `Skoosoft.Windows.WinTaskSchedulerManager.CreateOnLogon()` / `Delete()`, plus auto-migrate any leftover v0.x service via the Phase 2 service-uninstall code path.

The canonical pattern is already documented in the user's memory palace as `architecture/windows-services/windows-task-scheduler-startup-pattern` and was previously applied to billware/ConfigHub. **All five core requirements (TSK-01 through TSK-04, TSK-06) have a verified working implementation reference.** The Skoosoft.Windows package (≥ 8.0.7) wraps `Microsoft.Win32.TaskScheduler v2.12.2` (TaskScheduler community NuGet) and applies `InteractiveToken` + `TaskRunLevel.Highest` + `Hidden = true` internally.

**Primary recommendation:** Mirror the billware Task Scheduler pattern verbatim — Skoosoft wrapper handles all the trigger/logon-type/run-level decisions; phase work is mostly wiring (csproj, P/Invoke, NLog gate predicate, help text, replace schtasks.exe block).

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Skoosoft.Windows | ≥ 8.0.7 | `WinTaskSchedulerManager.CreateOnLogon` / `Delete` | Already a PackageReference; user's house wrapper for Task Scheduler. Requires ≥ 8.0.7 for these two methods. |
| Skoosoft.ProcessLib | * | Process enumeration + kill (TSK-05 stale-process cleanup) | Already referenced; preferred over raw `Process.GetProcessesByName` per project convention. |
| Microsoft.Win32.TaskScheduler | 2.12.2 (transitive) | Underlying COM TaskScheduler 2.0 wrapper | Pulled in by Skoosoft.Windows — do not reference directly. |
| NLog | 5.* | Existing logging — gate ConsoleTarget on `-console` | Already in csproj. Phase 1 established programmatic config. |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `kernel32.dll` AttachConsole | Win32 P/Invoke | Re-attach to parent shell stdout under WinExe | TSK-04: when `-c`, `-i`, `-u`, `--task`, `--removetask`, `--help` is active |

### Removed
| Library | Reason |
|---------|--------|
| Microsoft.Extensions.Hosting.WindowsServices | TSK-07 — no longer running as a service. The `Host.UseWindowsService()` extension goes away with it. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `WinTaskSchedulerManager` (Skoosoft) | Raw `schtasks.exe` shell-out (current code) | Current code IS schtasks.exe — that's what we're replacing. Raw schtasks is fragile (locale-dependent output parsing, no idempotency, no Hidden flag). |
| `WinTaskSchedulerManager` | Direct `Microsoft.Win32.TaskScheduler` COM API | More code, must apply InteractiveToken/Highest/Hidden manually. Skoosoft wrapper exists explicitly to avoid this. |
| `OutputType=WinExe` | Keep `Exe` + P/Invoke `ShowWindow(SW_HIDE)` | SW_HIDE has a visible flash race; WinExe is OS-level, no race. |
| `AttachConsole` | `AllocConsole` | AllocConsole creates a NEW console window (visible) — wrong UX for `-install` from inside a cmd. |

**Installation / version verification:**
```bash
# Skoosoft.Windows is private feed — version pinned via "*" already
# No new packages to install; only a removal:
dotnet remove src/FlaUI.Mcp/FlaUI.Mcp.csproj package Microsoft.Extensions.Hosting.WindowsServices
```

The `Skoosoft.Windows` `PackageReference Version="*"` will float to the newest available — confirm it resolves to ≥ 8.0.7 in `obj/project.assets.json` after restore. (Memory palace and billware phase history both confirm 8.0.7 as the version that introduced `CreateOnLogon` + `Delete` on `WinTaskSchedulerManager`.)

## Architecture Patterns

### Recommended Project Structure
```
src/FlaUI.Mcp/
├── Program.cs                      # entry point — modify CLI parser, register branches
├── FlaUI.Mcp.csproj                # OutputType=WinExe; remove WindowsServices pkg
├── Logging/
│   └── LoggingConfig.cs            # ConfigureLogging signature: (bool debug, string dir, bool console)
└── (no new files needed — keep wiring inline in Program.cs per existing top-level-statements style)
```

### Pattern 1: Task Scheduler Registration via Skoosoft Wrapper
**What:** Replace the raw `schtasks.exe` Process.Start block with a single Skoosoft call.
**When to use:** Both `--task` (and its `-install` / `-i` alias) and `--removetask` (and `-uninstall` / `-u` alias) branches.
**Example:**
```csharp
// Source: memory palace — main/architecture/windows-services/windows-task-scheduler-startup-pattern
using Skoosoft.Windows.Manager;

// --task / -install / -i branch
WinTaskSchedulerManager.CreateOnLogon(
    name: "FlaUI-MCP",                                    // task name (matches old service name)
    description: "Starts FlaUI-MCP at user logon",
    execFilePath: exeFilePath,
    execArguments: "");                                   // no args — runs default sse transport

// --removetask / -uninstall / -u branch
WinTaskSchedulerManager.Delete("FlaUI-MCP");              // idempotent — no-op if absent
```
Internally applies: `LogonTrigger()` (any user), `TaskLogonType.InteractiveToken`, `TaskRunLevel.Highest`, `Settings.Hidden = true`. This satisfies D-3a (any-user) and D-3b (no delay) by default — no extra parameters needed.

### Pattern 2: WinExe + AttachConsole CLI Plumbing
**What:** Process is built as WinExe (no console allocated when launched headless via Task Scheduler), but explicitly re-attaches to the parent shell's console when invoked from cmd/PowerShell with `-c` / `-i` / `-u` so the user sees install feedback.
**When to use:** Very early in Program.cs — after CLI parsing, before any `Console.WriteLine`, `LoggingConfig.ConfigureLogging`, or `Console.BufferWidth` call.
**Example:**
```csharp
// Source: memory palace — windows-task-scheduler-startup-pattern §3
using System.Runtime.InteropServices;

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool AttachConsole(int dwProcessId);
const int ATTACH_PARENT_PROCESS = -1;

// Right after CLI parsing — before any Console.* call or NLog config
if (console || install || uninstall || task || removeTask || helpRequested)
{
    AttachConsole(ATTACH_PARENT_PROCESS);
    // Return value intentionally ignored: false simply means no parent console
    // (e.g. launched by Task Scheduler) — Console.WriteLine becomes a no-op, fine.
}
```
**Note for `--help`:** the existing code calls `Console.WriteLine` inside the `--help` switch case BEFORE this attach point — phase plan must move `AttachConsole` ahead of the `--help` block, OR add it inside the `--help` case before printing.

### Pattern 3: NLog ConsoleTarget Gate Switch
**What:** Change the predicate in `LoggingConfig.ConfigureLogging` from `transport == "sse"` to `console` flag.
**When to use:** TSK-06 — the existing call site is `Program.cs:101`.
**Example:**
```csharp
// Before (current Program.cs:101):
LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport == "sse");

// After:
LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: console);
```
The `LoggingConfig.ConfigureLogging` method signature already accepts a bool — only the caller and possibly the parameter name change. No internal logic change required if the param is just forwarded into the conditional ConsoleTarget setup.

### Pattern 4: Debugger Guard with Self-PID Exclusion
**What:** When run under a debugger (F5 from Visual Studio / Rider), auto-set `console = true; debug = true;` and kill any leftover `FlaUI.Mcp` processes from previous debug sessions.
**When to use:** TSK-05 — runs unconditionally at startup (very early in Program.cs).
**Example:**
```csharp
// Source: memory palace pattern §4
if (Debugger.IsAttached)
{
    console = true;
    debug = true;

    var currentPid = Environment.ProcessId;
    foreach (var stale in Process.GetProcessesByName("FlaUI.Mcp")
                                 .Where(p => p.Id != currentPid))
    {
        try { stale.Kill(); stale.WaitForExit(5000); }
        catch { /* may have exited between enumeration and kill */ }
    }
}
```
**Critical:** the `Where(p => p.Id != currentPid)` is mandatory — without it, the process kills itself.

### Pattern 5: --help Reorganization (D-4)
**What:** Reorder `--help` so registration methods come first, runtime flags second, aliases last.
**Suggested layout:**
```
Usage: FlaUI.Mcp.exe [options]

Registration:
  --task              Register as scheduled task (runs at user logon, sees desktop)
  --removetask        Remove scheduled task

Runtime:
  --console, -c       Run in console mode (attach to parent shell, enable ConsoleTarget)
  --debug, -d         Enable debug-level logging (Debug.log)
  --silent, -s        Suppress prompts during registration
  --transport <type>  Transport: sse (default) or stdio
  --port <number>     SSE listen port (default: 3020)
  --help, -?          Show this help

Aliases (compatibility with v0.x service-based scripts):
  --install, -i       Same as --task
  --uninstall, -u     Same as --removetask
```
Existing port default in code is **3020** (Program.cs:24), not 8080 — current `--help` text shows 8080 incorrectly. Phase plan should fix this drift.

### Anti-Patterns to Avoid
- **Calling `schtasks.exe` directly** — locale-dependent output parsing, no Hidden flag, no idempotency. Replace entirely.
- **Calling `Console.WriteLine` before `AttachConsole`** — silently swallowed under WinExe; user sees no install feedback.
- **Forgetting `currentPid` exclusion in Debugger guard** — process self-terminates immediately after attach.
- **Setting `Console.BufferWidth` without checking attach succeeded** — throws `IOException` under WinExe with no parent console (TSK-08).
- **Leaving `Microsoft.Extensions.Hosting.WindowsServices` referenced** — pulls `Host.UseWindowsService()` paths that conflict with non-service execution.
- **Using `Environment.UserInteractive` as the headless gate** — under WinExe + Task Scheduler, this returns `true` even though there's no console. Guard sizing on a successful `AttachConsole` return value or a `console` flag instead.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Task Scheduler registration | `schtasks.exe /create` shell-out (current code) | `Skoosoft.Windows.WinTaskSchedulerManager.CreateOnLogon` | Locale-dependent stdout parsing, no Hidden flag, no idempotency, can't set InteractiveToken cleanly |
| Task deletion | `schtasks.exe /delete` shell-out (current code) | `WinTaskSchedulerManager.Delete` | Idempotency, error handling, exit-code interpretation |
| Console hiding under headless | P/Invoke `ShowWindow(handle, SW_HIDE)` | `<OutputType>WinExe</OutputType>` | SW_HIDE has a visible flash race; WinExe is OS-level pre-creation |
| Logon trigger plumbing | Direct `Microsoft.Win32.TaskScheduler` COM | Skoosoft wrapper | InteractiveToken + Highest + Hidden + LogonTrigger composition is verbose; wrapper is one line |
| Stale-process cleanup | Custom enumeration with race-prone WMI | `Process.GetProcessesByName` (LINQ filter own PID) + `Skoosoft.ProcessLib` | Standard library handles enumeration; PID exclusion is the only gotcha |

**Key insight:** The phase is almost entirely "remove ceremony, defer to Skoosoft". The current Program.cs has 70 lines of raw schtasks.exe wrangling (lines 165–235) that collapse to ~5 lines using `WinTaskSchedulerManager`.

## Runtime State Inventory

This is a refactor phase that changes a runtime-registered component (Windows Service → Scheduled Task). Inventory required:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — FlaUI-MCP is stateless beyond log files. Logs at `{AppBaseDirectory}\Log` are filename-stable. | None |
| Live service config | **Existing v0.x machines have a `FlaUI-MCP` Windows Service registered in SCM.** Not in git — lives in registry under `HKLM\SYSTEM\CurrentControlSet\Services\FlaUI-MCP`. **Also:** prior Phase 2 testing may have left a `FlaUI-MCP` scheduled task created via raw schtasks.exe (no Hidden flag, no InteractiveToken). | **D-1 auto-migration** in `--task` branch: call existing service-uninstall code path FIRST (idempotent, silent if absent), THEN create the scheduled task. Also: `WinTaskSchedulerManager.CreateOnLogon` should overwrite an existing task with the same name — verify this in Skoosoft docs / quick test, otherwise call `Delete` first. |
| OS-registered state | Windows Task Scheduler entry named `FlaUI-MCP` (from prior schtasks.exe run during Phase 2 testing). Windows Firewall rule `FlaUI-MCP` (Phase 2 SVC-07) — **keep**, still needed for SSE transport. | Replace task entry via Skoosoft create (with delete-first if needed). Firewall rule unchanged. |
| Secrets / env vars | None — no secrets in this project. | None |
| Build artifacts / installed packages | `bin/Release/net8.0-windows/FlaUI.Mcp.exe` will rebuild as a WinExe subsystem binary — different PE header subsystem byte. **Inno Setup output** (`Setup.iss` invoked via `CompileInnoSetup` AfterTargets target in csproj) ships an installer that may reference service install commands; phase work should review `Setup.iss` for `-install`/`-uninstall` invocations — these will keep working via D-2 aliases, but the comments / display names should reflect "Scheduled Task" not "Service". | Rebuild from clean. Review `Setup.iss` for cosmetic updates (not blocking — aliases preserve behavior). |

**Migration ordering critical:** D-1 mandates the service-uninstall happens BEFORE task creation. If the order is reversed (create task → uninstall service), an end user who Ctrl-C's between steps is left with both registered — confusing and broken (service tries to bind port 3020, task fails on logon).

## Common Pitfalls

### Pitfall 1: Console.* Calls Before AttachConsole Under WinExe
**What goes wrong:** User runs `FlaUI.Mcp.exe --install` from cmd, sees no output at all. Looks like the binary hung or did nothing.
**Why it happens:** WinExe subsystem means no console is allocated at process creation. `Console.Out` is `TextWriter.Null`. Any WriteLine before `AttachConsole(ATTACH_PARENT_PROCESS)` is silently dropped — including the `--help` block.
**How to avoid:** Place `AttachConsole` immediately after CLI parsing, before ANY `Console.*` call. The current code's `--help` case (Program.cs:58–73) calls WriteLine inline during parsing — restructure to defer printing until after the AttachConsole call, OR call AttachConsole inside the `--help` case before printing.
**Warning signs:** Manual UAT says "I ran `-install` and nothing happened, but the service got created." → output was eaten.

### Pitfall 2: Self-Termination in Debugger Guard
**What goes wrong:** Process kills itself at startup under F5 debug, the debugger immediately disconnects, `dotnet run` exits with code -1.
**Why it happens:** `Process.GetProcessesByName("FlaUI.Mcp")` returns the calling process too. Without `.Where(p => p.Id != Environment.ProcessId)`, the kill loop targets self.
**How to avoid:** Always include the PID exclusion. Test under VS debugger before merging.
**Warning signs:** "Process exited with code -1" immediately on F5; no log lines beyond the first NLog config call.

### Pitfall 3: Console Sizing IOException Under No-Console
**What goes wrong:** `Console.BufferWidth = 180` throws `System.IO.IOException: The handle is invalid` when run under Task Scheduler (no console attached) or via `WinExe` headless.
**Why it happens:** Current Program.cs:82 guards with `Environment.UserInteractive` — but UserInteractive returns `true` for a Task Scheduler InteractiveToken context too. The real check is "did AttachConsole succeed?" or equivalently "is there a real console handle?".
**How to avoid:** Switch the sizing guard from `if (!Debugger.IsAttached && Environment.UserInteractive)` to `if (console)` (i.e. only when the user explicitly asked for console mode and AttachConsole presumably succeeded). The existing try/catch around the assignment is a safety net but should not be the primary guard.
**Warning signs:** Error.log shows `IOException: The handle is invalid` at startup under Task Scheduler launch.

### Pitfall 4: NLog ConsoleTarget Predicate Drift
**What goes wrong:** ConsoleTarget fires under SSE transport in headless Task Scheduler context, NLog's internal write attempts to a non-existent console handle, log entries silently lost or NLog throws.
**Why it happens:** Current predicate (`transport == "sse"`, Program.cs:101) decoupled from the actual presence-of-console. Under Phase 3, transport defaults to sse and runs headless — predicate is wrong.
**How to avoid:** TSK-06 is exactly this fix. Predicate becomes `console` flag.
**Warning signs:** NLog internal log warnings about target failures; missing log lines that should have hit Error.log (because NLog short-circuits on target exception under some configs).

### Pitfall 5: Forgetting to Move Service-Stop Logic
**What goes wrong:** Program.cs:128–147 still tries to stop the running `FlaUI-MCP` service before console mode. After Phase 3, the service is gone — `ServiceManager.DoesServiceExist(ServiceName)` returns false, the block is dead code. But if D-1 auto-migration runs AFTER this block, there's a window where the service is still running while the new task is being created.
**Why it happens:** Order-of-operations drift between phases.
**How to avoid:** D-1's auto-uninstall (which includes stopping the service) must run inside the `--task` / `-install` branch, BEFORE the `WinTaskSchedulerManager.CreateOnLogon` call. The standalone "stop running service" block can either stay (idempotent if no service) or be removed; planner's call.
**Warning signs:** Port 3020 still bound after `--task` completes (old service still running).

### Pitfall 6: Skoosoft.Windows Version Drift
**What goes wrong:** `PackageReference Version="*"` resolves to a pre-8.0.7 build that doesn't have `CreateOnLogon` / `Delete` on `WinTaskSchedulerManager` — compile error.
**Why it happens:** Floating versions can cache stale; private feed may pin to older.
**How to avoid:** After `dotnet restore`, verify `obj/project.assets.json` shows Skoosoft.Windows ≥ 8.0.7. If it doesn't, pin: `Version="[8.0.7,)"`.
**Warning signs:** `error CS0117: 'WinTaskSchedulerManager' does not contain a definition for 'CreateOnLogon'` at build time.

## Code Examples

Verified patterns from memory palace (`main/architecture/windows-services/windows-task-scheduler-startup-pattern`) and prior billware/ConfigHub implementations.

### CSProj — Subsystem and Package Surgery (TSK-01, TSK-07)
```xml
<!-- Source: memory palace §2 + Phase 3 success criteria 1, 7 -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>             <!-- was: Exe -->
    <TargetFramework>net8.0-windows</TargetFramework>
    <!-- ...rest unchanged... -->
  </PropertyGroup>
  <ItemGroup>
    <!-- DELETED: <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.*" /> -->
    <PackageReference Include="Skoosoft.Windows" Version="*" />  <!-- already present -->
  </ItemGroup>
</Project>
```

### AttachConsole P/Invoke (TSK-04)
```csharp
// Source: memory palace §3
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(int dwProcessId);
    internal const int ATTACH_PARENT_PROCESS = -1;
}

// Call site — Program.cs, immediately after CLI parsing:
if (console || install || uninstall || task || removeTask || helpRequested)
{
    NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
}
```

### Task Create / Delete (TSK-02, TSK-03)
```csharp
// Source: memory palace §1 + canonical Skoosoft pattern
using Skoosoft.Windows.Manager;

const string TaskName = "FlaUI-MCP";

// --task / -i / --install branch
if (task || install)
{
    // D-1 auto-migration: uninstall legacy service if present (idempotent)
    if (ServiceManager.DoesServiceExist(ServiceName))
    {
        logger?.Info("Detected legacy FlaUI-MCP Windows Service — uninstalling before creating scheduled task");
        ServiceManager.Uninstall(ServiceName, silent: true);
    }

    WinTaskSchedulerManager.CreateOnLogon(
        name: TaskName,
        description: "Starts FlaUI-MCP at user logon (runs in user desktop session)",
        execFilePath: exeFilePath,
        execArguments: "");

    Console.WriteLine($"Scheduled task '{TaskName}' registered. Will start at next user logon.");
    logger?.Info("Scheduled task '{Task}' registered", TaskName);
    Environment.Exit(0);
}

// --removetask / -u / --uninstall branch
if (removeTask || uninstall)
{
    WinTaskSchedulerManager.Delete(TaskName);  // idempotent — no-op if absent
    Console.WriteLine($"Scheduled task '{TaskName}' removed.");
    logger?.Info("Scheduled task '{Task}' removed", TaskName);
    Environment.Exit(0);
}
```

### Debugger Guard (TSK-05)
```csharp
// Source: memory palace §4
if (Debugger.IsAttached)
{
    console = true;
    debug = true;

    var currentPid = Environment.ProcessId;
    foreach (var stale in Process.GetProcessesByName("FlaUI.Mcp")
                                 .Where(p => p.Id != currentPid))
    {
        try { stale.Kill(); stale.WaitForExit(5000); }
        catch (Exception ex) { /* race ok */ }
    }
}
```

### Sizing Guard (TSK-08)
```csharp
// Replaces Program.cs:82–94 — guard on `console` flag, not UserInteractive
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
        // redirected output, Windows Terminal, or AttachConsole failed silently
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Windows Service via `Host.UseWindowsService()` | Task Scheduler LogonTrigger via `WinTaskSchedulerManager.CreateOnLogon` | Phase 3 (this phase) | Process now runs in user desktop session — FlaUI/UIA3 can see windows |
| `OutputType=Exe` + P/Invoke `ShowWindow(SW_HIDE)` | `OutputType=WinExe` | Industry shift ~Win10 era | OS-level subsystem flag — no race, no flash |
| Raw `schtasks.exe` shell-out | `WinTaskSchedulerManager` (Skoosoft) wrapping `Microsoft.Win32.TaskScheduler` v2.12.2 | Skoosoft.Windows ≥ 8.0.7 (early 2026) | Locale-independent, idempotent, applies Hidden + InteractiveToken + Highest |
| `transport == "sse"` as ConsoleTarget gate | Explicit `-console` flag as gate | Phase 3 (TSK-06) | Decouples logging from transport choice |
| `Environment.UserInteractive` as headless gate | `console` flag (post-AttachConsole) | Phase 3 (TSK-08) | UserInteractive is true under InteractiveToken even without console |

**Deprecated/outdated:**
- `Microsoft.Extensions.Hosting.WindowsServices` package: removed (TSK-07). The `Host.UseWindowsService()` extension was already not in the current Program.cs (top-level statements use plain WebApplication, not generic Host) — package is dead reference.
- Raw `schtasks.exe` block in Program.cs:165–235: deleted, replaced by Skoosoft calls.

## Open Questions

1. **Does `WinTaskSchedulerManager.CreateOnLogon` overwrite an existing task with the same name, or throw?**
   - What we know: Memory palace §1 doesn't specify; Skoosoft wrapper is consistent with idempotency elsewhere.
   - What's unclear: Behavior when task already exists.
   - Recommendation: Planner should add `WinTaskSchedulerManager.Delete(TaskName)` immediately before `CreateOnLogon` defensively. Delete is idempotent (palace confirms), so this is safe even when no prior task exists. **Cost:** one extra Skoosoft call per registration. **Benefit:** unambiguous idempotency for D-1 / D-2.

2. **What's the exact public API surface — does `CreateOnLogon` accept a parameter for "specific user vs any user"?**
   - What we know: Memory palace example does NOT pass a user parameter, and notes "fires on any user logon" as the internal default.
   - What's unclear: Whether D-3a's "any user" requirement is the literal default or requires a flag.
   - Recommendation: Trust the palace example as-is. If the planner / executor encounters a method overload requiring a user, default to the installing user's identity (`WindowsIdentity.GetCurrent().Name`) and document the limitation in CHANGELOG. The Skoosoft signature can be confirmed via `goToDefinition` on `WinTaskSchedulerManager.CreateOnLogon` once the package is restored.

3. **Should the `Setup.iss` Inno Setup script be updated in this phase?**
   - What we know: csproj has `<Target Name="CompileInnoSetup" AfterTargets="Publish">` that compiles `Setup.iss`. Setup likely uses `-install` / `-uninstall` flags which now resolve to task registration via D-2 alias.
   - What's unclear: Whether display strings / shortcut names in `Setup.iss` reference "Service".
   - Recommendation: Out of scope for Phase 3 functional requirements (TSK-01..TSK-09). Add a planner note to review `Setup.iss` cosmetics, but do NOT block phase merge on it. Functionality is preserved by the D-2 alias contract.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK | Build | ✓ (assumed — repo builds today) | 8.x | — |
| Windows 10/11 | Task Scheduler 2.0 API | ✓ (Win11 Pro 26200, see env) | 10.0.26200 | — |
| Skoosoft.Windows ≥ 8.0.7 | TSK-02, TSK-03 | ✓ (already PackageReference, version `*` floats) | resolves at restore | Pin `Version="[8.0.7,)"` if `*` resolves lower |
| Skoosoft private NuGet feed | Package restore | ✓ (Phase 2 already used Skoosoft.ServiceHelperLib) | — | — |
| Inno Setup 6 | csproj `CompileInnoSetup` AfterTargets | Optional (only on publish) | — | Skip publish target during phase work; not required for build/test |
| Administrator privileges | `WinTaskSchedulerManager.CreateOnLogon` with `TaskRunLevel.Highest`, service uninstall | Required at runtime when running `--task` / `-install` | — | Document in `--help` and Setup.iss; manifest could request elevation |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | None detected in repo (no `*.Tests.csproj`, no `xunit`/`nunit`/`mstest` references) |
| Config file | none — see Wave 0 |
| Quick run command | `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug` (compile-as-smoke-test until tests exist) |
| Full suite command | `dotnet build && manual UAT script per success criteria` |
| Phase 2 precedent | Phase 2 used **manual UAT** (CHANGELOG mentions "complete UAT — 10/10 tests passed") — no automated test harness exists |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TSK-01 | OutputType=WinExe; no conhost.exe when launched headless | smoke | `findstr /C:"<OutputType>WinExe</OutputType>" src\FlaUI.Mcp\FlaUI.Mcp.csproj` | ✅ csproj |
| TSK-01 | No console window when launched by Task Scheduler | manual UAT | (verify visually after `--task` registration + logon) | ❌ manual |
| TSK-02 | `--task` creates task via Skoosoft (not schtasks.exe) | smoke + manual | `findstr /C:"WinTaskSchedulerManager.CreateOnLogon" src\FlaUI.Mcp\Program.cs && findstr /V /C:"schtasks" src\FlaUI.Mcp\Program.cs` | ✅ Program.cs |
| TSK-02 | Task actually appears in Task Scheduler with InteractiveToken/Highest/Hidden | manual UAT | `schtasks /query /tn FlaUI-MCP /v /fo LIST` (post-registration) | ❌ manual |
| TSK-03 | `--removetask` deletes via Skoosoft, idempotent | manual UAT | run `--removetask` twice, both exit 0 | ❌ manual |
| TSK-04 | AttachConsole called before any Console.* under -c/-i/-u | static check | `findstr /C:"AttachConsole" src\FlaUI.Mcp\Program.cs` | ✅ Program.cs |
| TSK-04 | Output visible when running `-install` from cmd | manual UAT | run from cmd, expect WriteLine output | ❌ manual |
| TSK-05 | Debugger.IsAttached enables -c -d and kills stale procs (excl. own PID) | unit-ish (debug only) | F5 from VS twice — second instance kills first, doesn't kill itself | ❌ manual under debugger |
| TSK-06 | NLog ConsoleTarget gated on `console`, not transport | static check | `findstr /C:"enableConsoleTarget: console" src\FlaUI.Mcp\Program.cs` | ✅ Program.cs (after edit) |
| TSK-07 | Microsoft.Extensions.Hosting.WindowsServices package removed | static check | `findstr /V /C:"Microsoft.Extensions.Hosting.WindowsServices" src\FlaUI.Mcp\FlaUI.Mcp.csproj` (must NOT match) | ✅ csproj |
| TSK-08 | Console sizing skipped under headless | smoke | static review: sizing block guarded by `if (console)` | ✅ Program.cs |
| TSK-08 | No IOException at startup under Task Scheduler | manual UAT | check Error.log after Task Scheduler-launched run | ❌ manual |
| TSK-09 | --help text reflects Task Scheduler primacy per D-4 layout | static check + manual review | `FlaUI.Mcp.exe --help` → eyeball ordering: Registration > Runtime > Aliases | ❌ manual |

### Sampling Rate
- **Per task commit:** `dotnet build src\FlaUI.Mcp\FlaUI.Mcp.csproj -c Debug` (must succeed, zero warnings on touched lines)
- **Per wave merge:** `dotnet build` + the four static `findstr` smoke checks above
- **Phase gate:** Manual UAT script covering: (a) `-install` from elevated cmd shows registration message, (b) reboot/logoff-logon → server starts in user session (Task Manager: Session 1, no conhost.exe), (c) MCP client connects on port 3020, (d) FlaUI can list desktop windows, (e) `-uninstall` removes task and is idempotent on second run, (f) on a machine with leftover v0.x service, `-install` auto-migrates silently with one info log entry.

### Wave 0 Gaps
- [ ] No automated test framework exists. Phase 3 follows Phase 2's manual UAT precedent — **do not introduce xunit in this phase** (out of scope; would balloon work). A separate future phase can add `FlaUI.Mcp.Tests`.
- [ ] **Manual UAT checklist** must be authored as part of phase plan, with explicit step-by-step (see Phase 2's UAT artifact under `.gsd/milestones/1.0/2-service-hardening/` as template).
- [ ] **Static smoke check shell** — author a 10-line `verify.cmd` or `verify.ps1` running the `findstr` checks above so /gsd:verify-work has a one-shot.

*(Phase 2 set the precedent of manual UAT with documented results — Phase 3 can follow the same pattern. Adding test infrastructure is a separate concern that should be its own phase / milestone.)*

## Sources

### Primary (HIGH confidence)
- **Memory palace:** `main/architecture/windows-services/windows-task-scheduler-startup-pattern` — canonical pattern, all six sub-patterns (Skoosoft API, WinExe, AttachConsole, Debugger guard, Setup.iss, startup sequence)
- **Memory palace:** `main/projects/billware/phase-history` §08-01, §08-GAP-01 — confirms Skoosoft.Windows ≥ 8.0.7, real-world successful application of identical pattern
- **Memory palace:** `main/projects/config-hub/stack-config-hub` — confirms Skoosoft.Windows is the standard Task Scheduler abstraction in user's ecosystem
- **Repo source:** `src/FlaUI.Mcp/Program.cs` (current state — what needs replacing) and `src/FlaUI.Mcp/FlaUI.Mcp.csproj` (current package list, OutputType)
- **Phase context:** `.gsd/milestones/1.0/3-task-scheduler-startup/3-CONTEXT.md` (locked decisions D-1 through D-4)

### Secondary (MEDIUM confidence)
- **Repo phase 2 history:** Phase 2 CHANGELOG entries (`docs(2): complete UAT — 10/10 tests passed with 4 issues fixed`) — confirms manual UAT pattern is the project's testing convention.
- **Repo csproj:** `<Target Name="CompileInnoSetup" AfterTargets="Publish">` — Setup.iss exists and runs at publish time; confirmed presence of installer integration.

### Tertiary (LOW confidence)
- None — all critical claims are HIGH confidence from memory palace and direct repo inspection.

### Memory Persistence
The pattern note `main/architecture/windows-services/windows-task-scheduler-startup-pattern` already covers all reusable knowledge from this research. **No new palace notes needed** — this phase consumes existing knowledge rather than producing new general patterns. Phase-specific decisions (D-1 auto-migration, D-2 alias mapping) live in CONTEXT.md and don't need palace persistence (project-specific, not reusable).

If the planner / executor surfaces a new gotcha during implementation (e.g. a Skoosoft API quirk on this version), it should be added to the palace note as an additional observation under `#windows-services`.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Skoosoft.Windows already referenced; version requirement (≥ 8.0.7) confirmed from two independent palace sources (billware phase history + canonical pattern note).
- Architecture: HIGH — pattern is verbatim from a working production reference (billware) with identical concerns (WinExe, console attach, NLog gating, Debugger guard).
- Pitfalls: HIGH — six pitfalls all derived from documented palace gotchas plus direct reading of current Program.cs (e.g., the `transport == "sse"` predicate drift is in the live source).
- API surface (open question 1, 2): MEDIUM — Skoosoft method signatures inferred from palace example; planner should `goToDefinition` post-restore to confirm. Defensive Delete-before-Create recommendation handles uncertainty cheaply.

**Research date:** 2026-04-26
**Valid until:** 2026-05-26 (30 days — pattern is stable; only Skoosoft.Windows version drift would invalidate)

## RESEARCH COMPLETE
