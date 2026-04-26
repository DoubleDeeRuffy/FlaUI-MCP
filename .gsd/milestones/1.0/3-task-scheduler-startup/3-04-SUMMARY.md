---
phase: 3-task-scheduler-startup
plan: 04
subsystem: startup
tags: [debugger, f5, stale-process-kill, own-pid-exclusion]
requires: [3-03]
provides: [f5-auto-flags, stale-process-cleanup]
affects: [src/FlaUI.Mcp/Program.cs]
tech_added: []
patterns: [debugger-isattached-guard, getprocessesbyname, own-pid-exclusion-linq]
files_created: []
files_modified:
  - src/FlaUI.Mcp/Program.cs
decisions:
  - Placed Debugger.IsAttached guard AFTER --help exit and BEFORE Encoding.RegisterProvider, so --help does not trigger stale-process kills and console=true/debug=true assignments are visible to the sizing block and ConfigureLogging.
  - Used Environment.ProcessId (not Process.GetCurrentProcess().Id) for own-PID lookup — cheaper and idiomatic on .NET 8.
  - Did NOT tighten kill scope to same-session/same-user (CONTEXT D-5) — solo-developer context, kill-by-name across all sessions is the literal requirement.
metrics:
  duration_minutes: 2
  tasks_completed: 1
  files_modified: 1
completed: 2026-04-26
requirements_completed: [TSK-05]
---

# Phase 3 Plan 04: Debugger Auto-Flags + Stale-Process Kill Summary

F5 from Visual Studio / Rider now auto-enables `--console` and `--debug`, then kills any leftover `FlaUI.Mcp` processes from previous debug sessions while strictly excluding the current PID — preventing the self-kill pitfall that would disconnect the debugger and exit `dotnet run` with code -1.

## Tasks Completed

| # | Name                                                    | Status |
| - | ------------------------------------------------------- | ------ |
| 1 | Add Debugger.IsAttached guard with stale-process kill   | done   |

## Implementation

- Inserted new block "1c. Debugger guard" in `src/FlaUI.Mcp/Program.cs` immediately after the `helpRequested` exit block (line 95) and before `Encoding.RegisterProvider`.
- Block sets `console = true; debug = true;` under `Debugger.IsAttached`.
- Captures `Environment.ProcessId` into `currentPid`, enumerates `Process.GetProcessesByName("FlaUI.Mcp")`, filters via LINQ `.Where(p => p.Id != currentPid)`.
- Each stale process is `Kill()`-ed inside try/catch with `WaitForExit(5000)`; race conditions during enumeration are silently swallowed (intended).
- No new using directives required: `System.Diagnostics` already at line 1, `System.Linq` available via `<ImplicitUsings>enable</ImplicitUsings>`.

## Verification

- `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` — succeeded (3 pre-existing warnings, 0 errors).
- Source confirms: `Debugger.IsAttached`, `Environment.ProcessId`, `GetProcessesByName("FlaUI.Mcp")`, `.Where(p => p.Id != currentPid)`, `stale.Kill()`, `stale.WaitForExit(5000)` all present.
- Manual UAT (deferred to Plan 07): F5 with stale FlaUI.Mcp.exe running → confirm stale PID killed via Task Manager and own debugger session survives.

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check: PASSED

- FOUND: src/FlaUI.Mcp/Program.cs (modified)
- FOUND: Debugger.IsAttached block with own-PID exclusion
- Build: green
