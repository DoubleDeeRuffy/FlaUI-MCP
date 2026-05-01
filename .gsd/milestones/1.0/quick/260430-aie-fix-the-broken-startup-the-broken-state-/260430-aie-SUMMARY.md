---
phase: 260430-aie
plan: "01"
subsystem: startup
tags: [startup, process-kill, port-binding, kestrel, task-scheduler, regression-fix]
requires:
  - Phase-3 (Task Scheduler relaunch design — TSK-05 stale-kill primitive)
  - Phase-4 (streamable-http-transport on port 3020 — the contended port)
provides:
  - "Unconditional stale-instance kill on every non-help startup"
  - "Hardened Process disposal (IDisposable) and shorter WaitForExit window (2 s)"
affects:
  - src/FlaUI.Mcp/Program.cs
tech_stack:
  added: []
  patterns:
    - "Process.GetProcessesByName + PID exclusion + WaitForExit"
    - "using (Process) for IDisposable handle release"
    - "Pre-NLog diagnostics via Console.Error.WriteLine inside nested try/catch"
key_files:
  created: []
  modified:
    - src/FlaUI.Mcp/Program.cs
decisions:
  - "Stale-kill block lifted out of Debugger.IsAttached branch into its own unconditional top-level block (D-Q1)"
  - "WaitForExit timeout reduced from 5000 ms to 2000 ms (D-Q2)"
  - "Each Process wrapped in `using` to release the OS handle (research Q1d)"
  - "ConsoleTarget gating untouched: enableConsoleTarget = transport != \"stdio\" (D-Q3)"
  - "Kill block is inline (not a helper) — keeps top-level statement layout flat"
  - "No new CLI flags, no CliOptions changes, no new test files (research Q4 — Process API is unmockable without an out-of-scope abstraction refactor)"
metrics:
  duration_minutes: 7
  completed: 2026-05-01
  tasks_completed: 1
  files_modified: 1
---

# Quick Task 260430-aie Plan 01: stale-process kill on every startup — Summary

One-liner: Lifts the stale-FlaUI.Mcp kill loop out of `Debugger.IsAttached` into an unconditional top-level block so headless / Task-Scheduler / non-`-c` launches no longer collide with prior instances on Kestrel port 3020, while also disposing each `Process` and shortening `WaitForExit` to 2 s.

## What Changed

### `src/FlaUI.Mcp/Program.cs`

The previously broken behavior was: stale-kill only ran inside `if (Debugger.IsAttached)`, so a Task-Scheduler-launched (or headless / `-c`-launched) instance never freed port 3020 from a prior instance. The new instance silently failed to bind with no console attached to surface the error — the user-visible "broken startup."

Diff applied:

1. **Removed the stale-kill loop from inside `if (Debugger.IsAttached)`** (former lines 62-82, comment banner `1c. Debugger guard: F5 ... kills stale FlaUI.Mcp procs (TSK-05)`).

2. **Inserted a new unconditional top-level block** at lines 62-85 (banner `1c. Always kill stale FlaUI.Mcp instances (excluding own PID) before any port-binding work.`):

   ```csharp
   {
       var currentPid = Environment.ProcessId;
       foreach (var stale in Process.GetProcessesByName("FlaUI.Mcp")
                                    .Where(p => p.Id != currentPid))
       {
           using (stale)
           {
               try
               {
                   stale.Kill();
                   stale.WaitForExit(2000);
               }
               catch (Exception ex)
               {
                   try { Console.Error.WriteLine($"FlaUI.Mcp: stale-kill skipped (pid={stale.Id}): {ex.Message}"); } catch { }
               }
           }
       }
   }
   ```

3. **Slimmed `if (Debugger.IsAttached)`** (lines 87-92, banner `1d. Debugger guard: F5 from VS auto-enables -c -d (TSK-05; stale-kill is now unconditional above)`) to its remaining responsibility:

   ```csharp
   if (Debugger.IsAttached)
   {
       console = true;
       debug = true;
   }
   ```

Three behavioral changes vs. the prior code:

- **Scope:** kill now runs on every startup except `--help`. The `--help` branch already calls `Environment.Exit(0)` at line 59 so no explicit guard is needed.
- **Timeout:** `WaitForExit(5000)` → `WaitForExit(2000)` per locked decision D-Q2 (research Q3 confirms 2 s is ample; listening sockets are released by the kernel as soon as `TerminateProcess` finalizes, with no TIME_WAIT applying to LISTEN-state sockets).
- **Resource hygiene:** each `Process` is now `using (stale) { ... }`-wrapped, releasing the OS handle even when the inner try block throws (research Q1d).

Diagnostic strategy: NLog is not yet configured at this point in startup (logging is set up at line ~117), so any kill failure (race, AccessDenied if UAC mismatch) is reported via `Console.Error.WriteLine` inside an outer try/catch that itself swallows exceptions, because stderr may not be attached when launched via Task Scheduler.

What was **NOT** touched (intentional):
- `src/FlaUI.Mcp/CliOptions.cs` — unchanged (FIX-04 / D-Q3 scope guard).
- `src/FlaUI.Mcp/Logging/LoggingConfig.cs` — unchanged (D-Q3: `enableConsoleTarget: transport != "stdio"` stays).
- No new CLI flags, no new tests.

## Verification Results

### Automated

| Check | Command | Result |
|-------|---------|--------|
| Build | `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo --source https://api.nuget.org/v3/index.json` | **0 errors**, 3 pre-existing warnings (MCP9004 deprecation in `Mcp/Http/HttpTransport.cs:92`; CS0414 unused fields in `Core/SessionManager.cs:16` and `Mcp/McpServer.cs:13`) — none introduced by this edit. |
| CliOptions tests | `dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --no-restore --filter "FullyQualifiedName~CliParserTests"` | **9 / 9 passed**, 0 failed, 0 skipped, duration 11 ms. FIX-04 (no parsing regression) holds. |

**NuGet auth note:** the project's `nuget.config` includes a private TeamCity feed (`teamcity.skoosoft.de_v2`) that returned `401 Unauthorized` during the implicit `restore` of the `dotnet build` / `dotnet test` commands. The wildcard floating versions in the csproj (`NLog 5.*`, `NLog.Web.AspNetCore 5.*`) re-trigger version resolution against every enabled source on each restore. Restoring with `--source https://api.nuget.org/v3/index.json` (limiting to nuget.org for the run) sidesteps the auth gate, and once the assets are written under `obj/`, subsequent `dotnet test --no-restore` runs use the cached graph. This is a pre-existing infrastructure auth condition, not a regression caused by the stale-kill edit.

### Static spot-checks (six grep assertions from PLAN `<verify>`)

| # | Check | Result |
|---|-------|--------|
| A | Kill block OUTSIDE Debugger branch — `GetProcessesByName` line < `Debugger.IsAttached` line | **PASS** — `GetProcessesByName` at line 69, `Debugger.IsAttached` at line 88. |
| B | `WaitForExit(2000)`, no 5000 anywhere | **PASS** — single match `stale.WaitForExit(2000);` at line 77; `grep -n "5000"` returns no results. |
| C | `GetProcessesByName("FlaUI.Mcp")` (no `.exe`) | **PASS** — line 69 reads `Process.GetProcessesByName("FlaUI.Mcp")`. |
| D | `using (stale)` present | **PASS** — line 72. |
| E | Debugger body slim: only `console = true; debug = true;` — no Process API in branch | **PASS** — lines 88-92 contain exactly `if (Debugger.IsAttached) { console = true; debug = true; }`; no `GetProcessesByName` / `Kill` / `WaitForExit` inside the branch. |
| F | ConsoleTarget gating untouched | **PASS** — line 117 unchanged: `LoggingConfig.ConfigureLogging(debug, logDirectory, enableConsoleTarget: transport != "stdio");` |

### Manual UAT — deferred to verifier

The five UAT scenarios documented in `<verification>` of the PLAN (headless relaunch, explicit `-c` second launch, `--help` does not kill, clean start, Debugger F5 path) are the responsibility of `/gsd:verify-work`. They require a Windows host with the binary published / `dotnet run`-able and cannot be validated from automated unit tests because the Process API is not mockable without an out-of-scope abstraction refactor (research Q4).

## Deviations from Plan

None. Plan executed exactly as written:
- The optional `if (!helpRequested)` guard around the kill block was deliberately omitted (the `--help` branch already calls `Environment.Exit(0)` at line 59); the surrounding comment banner makes the invariant explicit. The plan flagged this as Claude's discretion — both shapes were declared correct.
- All six static spot-checks pass.
- All locked decisions (D-Q1, D-Q2, D-Q3) were honored.

## Authentication Gates Encountered

The implicit NuGet restore against the private `teamcity.skoosoft.de_v2` source returned `401 Unauthorized`. This is a pre-existing build infrastructure condition (the floating `5.*` NLog versions re-resolve against every source on each restore), not a code issue. Working around it by passing `--source https://api.nuget.org/v3/index.json` was sufficient — both the build and the CliParserTests test run completed cleanly. Documented here for the verifier; no action needed for this plan.

## Files Touched

- `src/FlaUI.Mcp/Program.cs` — 22 insertions, 12 deletions in commit `210c7b2`.

## Commits

- `210c7b2` — `1.0-quick-260430-aie-fix: stale-process kill on every startup`

## Pass Criteria Status

- [x] `src/FlaUI.Mcp/Program.cs` compiles (`dotnet build` exit 0).
- [x] Existing CliOptions xunit tests pass (9/9 in `CliParserTests`).
- [x] Stale-kill block is OUTSIDE `if (Debugger.IsAttached)` (line 69 < line 88).
- [x] `Process.GetProcessesByName("FlaUI.Mcp")` — no `.exe`.
- [x] Each `Process` wrapped in `using`.
- [x] `WaitForExit(2000)`; no 5000 anywhere.
- [x] `Debugger.IsAttached` body contains only `console = true; debug = true;`.
- [x] `enableConsoleTarget: transport != "stdio"` unchanged.
- [ ] Manual UAT scenarios 1-5 — deferred to `/gsd:verify-work`.

## Self-Check: PASSED

- File `src/FlaUI.Mcp/Program.cs` exists and contains the new block at lines 62-85, slimmed Debugger branch at lines 87-92.
- Commit `210c7b2` exists in `git log`:
  ```
  210c7b2 1.0-quick-260430-aie-fix: stale-process kill on every startup
  ```
- All six static spot-checks (A-F) re-verified post-commit.
- `dotnet test --filter CliParserTests` re-confirmed 9/9 passing.
