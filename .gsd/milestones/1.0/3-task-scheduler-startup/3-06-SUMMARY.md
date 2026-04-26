---
phase: 3-task-scheduler-startup
plan: 06
subsystem: cli-help
tags: [help-text, cli, d-4-layout, verification]
requires: [3-05]
provides: [d-4-help-layout-verified]
affects: [src/FlaUI.Mcp/Program.cs]
tech_added: []
patterns: [no-op-verification]
files_created: []
files_modified: []
decisions:
  - No-op verification — Plan 03 pre-emptively wrote the canonical D-4 help layout, so Plan 06 confirms compliance without edits. Sections present in correct order (Registration > Runtime > Aliases), port shows 3020, no `default: 8080` drift, and the block ends with `Environment.Exit(0)`.
metrics:
  duration_minutes: 2
  tasks_completed: 1
  files_modified: 0
completed: 2026-04-26
requirements_completed: [TSK-09]
---

# Phase 3 Plan 06: Final --help Layout Verification Summary

TSK-09 verified: the `--help` block in `src/FlaUI.Mcp/Program.cs` (lines 73-95) matches the canonical D-4 layout exactly — Registration first, Runtime middle, Aliases last; port 3020; no edits required.

## Tasks Completed

| # | Name                                              | Status |
| - | ------------------------------------------------- | ------ |
| 1 | Verify and finalize --help text per D-4           | done (no-op verification) |

## Implementation

No code changes. Plan 03 had already written the help block in the final D-4 form (decision recorded in 3-03-SUMMARY.md: "Help text is now deferred via helpRequested flag and printed after AttachConsole, so --help works correctly under WinExe", with corrected port default 3020 and Plan 06's Registration/Runtime/Aliases structure).

Line-by-line check against canonical layout (RESEARCH §Pattern 5):

1. Section headers in order: `Registration:` (line 79), `Runtime:` (line 83), `Aliases (compatibility with v0.x service-based scripts):` (line 91). PASS.
2. Registration contains `--task` (80), `--removetask` (81). PASS.
3. Runtime contains `--console, -c` (84), `--debug, -d` (85), `--silent, -s` (86), `--transport <type>` (87), `--port <number>` (88), `--help, -?` (89). PASS.
4. Aliases maps `--install, -i → Same as --task` (92), `--uninstall, -u → Same as --removetask` (93). PASS.
5. Port mention: `default: 3020` (line 88). No `default: 8080`. PASS.
6. Block ends with `Environment.Exit(0);` (line 94). PASS.

## Verification

- `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` — succeeded (3 pre-existing warnings, 0 errors).
- `verify.cmd` — output `=== ALL GREEN ===`. Wave 0 gate fully green: WinExe present, WindowsServices absent, WinTaskSchedulerManager present, AttachConsole present, schtasks absent.
- All 6 D-4 layout checks pass against current Program.cs.

## Deviations from Plan

None — plan executed exactly as written. As anticipated by the plan ("If all checks pass, this task is verification-only — no edits needed"), this was a no-op verification.

## Self-Check: PASSED

- FOUND: src/FlaUI.Mcp/Program.cs (unchanged; help block lines 73-95 verified)
- FOUND: verify.cmd ALL GREEN
- Build: green
