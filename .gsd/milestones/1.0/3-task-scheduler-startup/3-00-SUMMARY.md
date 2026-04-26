---
phase: 3
plan: 00
subsystem: validation-infra
tags: [phase-3, wave-0, validation, uat]
requires: []
provides:
  - verify.cmd gate (build + 5 findstr smoke checks)
  - UAT-CHECKLIST.md (10 manual scenarios + bonus migration)
affects: []
tech_stack_added: []
patterns: [batch-script-errorlevel-inversion, manual-uat-checklist]
key_files_created:
  - verify.cmd
  - .gsd/milestones/1.0/3-task-scheduler-startup/UAT-CHECKLIST.md
key_files_modified: []
decisions:
  - Gate ships intentionally red — passes only after Plans 01..06 satisfy all 5 smoke checks
  - 5 findstr checks (4 must-match + 1 must-NOT-match for raw schtasks) chosen to keep TSK-01/02/04/07 covered statically; runtime/UI behaviors deferred to manual UAT
metrics:
  tasks: 2
  files_changed: 2
  duration_min: ~5
completed: 2026-04-26
---

# Phase 3 Plan 00: Validation Infrastructure Summary

Wave 0 ships the validation gate (`verify.cmd`) and human UAT checklist that downstream plans 01-06 will be measured against.

## What Shipped

- **`verify.cmd`** at repo root — `@echo off` batch script that runs `dotnet build -c Release` followed by 5 `findstr` smoke checks. Uses correct `errorlevel` inversion: `if errorlevel 1` for must-match cases, `if not errorlevel 1` for must-NOT-match (`Microsoft.Extensions.Hosting.WindowsServices` package, raw `schtasks` shell-out). Exits 0 on all-green, 1 with `FAIL: <check>` message on any failure.
- **`.gsd/milestones/1.0/3-task-scheduler-startup/UAT-CHECKLIST.md`** — 10 numbered scenarios cross-referencing all 9 TSK requirement IDs (TSK-01..TSK-09). Each scenario has Goal / Steps / Pass criteria. Bonus D-1 auto-migration scenario documented as optional. Results-summary table appended for human use during Plan 07 UAT.

## Smoke Checks (verify.cmd)

| # | Check | Type | Requirement |
|---|-------|------|-------------|
| 1 | `<OutputType>WinExe</OutputType>` in csproj | must-match | TSK-01 |
| 2 | `Microsoft.Extensions.Hosting.WindowsServices` in csproj | must-NOT-match | TSK-07 |
| 3 | `WinTaskSchedulerManager` in Program.cs | must-match | TSK-02 |
| 4 | `AttachConsole` in Program.cs | must-match | TSK-04 |
| 5 | `schtasks` in Program.cs | must-NOT-match | TSK-02 |

## Deviations from Plan

None — plan executed exactly as written.

## Verification

- Both files exist at specified paths.
- UAT-CHECKLIST.md contains 20 TSK-* references covering TSK-01 through TSK-09.
- `verify.cmd` is expected to FAIL against current (unedited) code — that is the design (gate ships red; turns green only after Plans 01-06).

## Self-Check: PASSED

- `verify.cmd` — FOUND (1496 bytes)
- `.gsd/milestones/1.0/3-task-scheduler-startup/UAT-CHECKLIST.md` — FOUND (5675 bytes)
- All 9 TSK IDs present in UAT-CHECKLIST.md
