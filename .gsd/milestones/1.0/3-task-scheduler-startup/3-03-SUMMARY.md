---
phase: 3-task-scheduler-startup
plan: 03
subsystem: startup
tags: [attach-console, pinvoke, headless, cli, win-exe]
requires: [3-02]
provides: [parent-shell-console-output, headless-safe-sizing]
affects: [src/FlaUI.Mcp/Program.cs]
tech_added: []
patterns: [pinvoke-attachconsole, deferred-help-print, console-flag-gating]
files_created: []
files_modified:
  - src/FlaUI.Mcp/Program.cs
decisions:
  - Used fully-qualified System.Runtime.InteropServices.DllImport on the NativeMethods class to avoid adding a new using directive that conflicts with top-level statements.
  - Help text is now deferred via helpRequested flag and printed after AttachConsole, so --help works correctly under WinExe.
  - Sizing block guard switched from (!Debugger.IsAttached && Environment.UserInteractive) to (console). Under F5 the Plan-04 Debugger guard sets console=true, so no regression.
metrics:
  duration_minutes: 4
  tasks_completed: 1
  files_modified: 1
completed: 2026-04-26
requirements_completed: [TSK-04, TSK-08]
---

# Phase 3 Plan 03: AttachConsole + Headless-Safe Sizing Summary

AttachConsole(ATTACH_PARENT_PROCESS) re-attaches the WinExe-built process to the parent shell's console for CLI feedback, and the Console.BufferWidth sizing block is now gated on the explicit `--console` flag instead of `Environment.UserInteractive`, eliminating the IOException crash when launched headlessly by Task Scheduler.

## Tasks Completed

| # | Name                                                              | Status |
| - | ----------------------------------------------------------------- | ------ |
| 1 | Add NativeMethods, restructure --help, AttachConsole gate, sizing | done   |

## Implementation

- Added `helpRequested` local flag (Program.cs:25).
- Replaced inline `--help` `Console.WriteLine` block with `helpRequested = true; break;`.
- Added AttachConsole gate immediately after the CLI for-loop (Program.cs:67-73): triggers on `console || install || uninstall || task || removeTask || helpRequested`.
- Inserted deferred help printing block after the AttachConsole gate, with corrected port default (3020) and Plan 06's Registration/Runtime/Aliases structure.
- Replaced sizing guard `if (!Debugger.IsAttached && Environment.UserInteractive)` with `if (console)`.
- Appended `internal static class NativeMethods` at the bottom of Program.cs with `AttachConsole` P/Invoke and `ATTACH_PARENT_PROCESS = -1` constant, using fully-qualified `System.Runtime.InteropServices.DllImport`.

## Verification

- `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` — succeeded (3 pre-existing warnings, 0 errors).
- Grep confirms `NativeMethods.AttachConsole`, `ATTACH_PARENT_PROCESS`, and `if (console)` are present.
- Grep confirms the BufferWidth sizing block no longer references `Environment.UserInteractive` (the remaining UserInteractive reference at line 148 is the unrelated SVC-08 service-stop block, unchanged).

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check: PASSED

- FOUND: src/FlaUI.Mcp/Program.cs (modified)
- FOUND: NativeMethods class, AttachConsole gate, deferred --help, sizing guard switched to `if (console)`
- Build: green
