---
phase: 3-task-scheduler-startup
plan: 05
subsystem: logging
tags: [nlog, console-target, headless, task-scheduler]
requires: [3-04]
provides: [console-flag-gated-logging]
affects: [src/FlaUI.Mcp/Program.cs]
tech_added: []
patterns: [user-intent-vs-transport-decoupling]
files_created: []
files_modified:
  - src/FlaUI.Mcp/Program.cs
decisions:
  - Gate ConsoleTarget on the explicit `console` flag instead of `transport == "sse"`. ConsoleTarget reflects UX intent (-c / --console), not transport choice. Under default headless Task Scheduler launch (transport=sse, no console), ConsoleTarget is now correctly skipped — eliminating NLog Pitfall-4 silent-fail warnings.
metrics:
  duration_minutes: 1
  tasks_completed: 1
  files_modified: 1
completed: 2026-04-26
requirements_completed: [TSK-06]
---

# Phase 3 Plan 05: ConsoleTarget Gate on Console Flag Summary

NLog ConsoleTarget is now attached IFF the user explicitly passes `-c` / `--console`, decoupling log-target choice from transport choice and preventing NLog Pitfall-4 silent failures under headless Task Scheduler launch.

## Tasks Completed

| # | Name                                                                    | Status |
| - | ----------------------------------------------------------------------- | ------ |
| 1 | Switch ConfigureLogging predicate from transport-equality to console-flag | done   |

## Implementation

- `src/FlaUI.Mcp/Program.cs:142` — changed third argument of `LoggingConfig.ConfigureLogging` from `enableConsoleTarget: transport == "sse"` to `enableConsoleTarget: console`.
- No other call sites or signatures touched. `LoggingConfig.ConfigureLogging` definition unchanged.
- Plan 04's `Debugger.IsAttached` block (sets `console = true` before this call) ensures F5 debug sessions still attach ConsoleTarget.

## Verification

- `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` — succeeded (3 pre-existing warnings, 0 errors).
- Grep confirms: only `enableConsoleTarget: console` present at Program.cs:142; old `enableConsoleTarget: transport == "sse"` predicate gone.
- Manual UAT (deferred to Plan 07 Scenario 7): scheduled-task launch without `-c` → tail Error.log → expect no NLog ConsoleTarget warnings.

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check: PASSED

- FOUND: src/FlaUI.Mcp/Program.cs (modified, line 142)
- FOUND: commit b69232e (1.0-3-feat: task-scheduler-startup)
- Build: green
