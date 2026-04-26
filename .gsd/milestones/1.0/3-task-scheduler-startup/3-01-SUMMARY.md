---
phase: 3
plan: 01
subsystem: build
tags: [csproj, winexe, packages]
requires: [3-00]
provides: [winexe-output, skoosoft-windows-8.0.7]
affects: [src/FlaUI.Mcp/FlaUI.Mcp.csproj]
tech_stack:
  added: []
  patterns: [winexe-subsystem]
key_files:
  created: []
  modified:
    - src/FlaUI.Mcp/FlaUI.Mcp.csproj
decisions:
  - Skoosoft.Windows floats via Version="*" — resolved to 8.0.7 (>= required for CreateOnLogon/Delete)
metrics:
  duration_minutes: 1
  completed: 2026-04-26
requirements: [TSK-01, TSK-07]
---

# Phase 3 Plan 01: csproj WinExe + Package Cleanup Summary

**One-liner:** Flipped OutputType to WinExe and removed dead Microsoft.Extensions.Hosting.WindowsServices reference; Skoosoft.Windows resolves to 8.0.7 (precondition for Plan 02 task scheduler integration).

## What Changed

- `src/FlaUI.Mcp/FlaUI.Mcp.csproj`:
  - `<OutputType>Exe</OutputType>` → `<OutputType>WinExe</OutputType>` (TSK-01: prevents console window allocation when launched headless by Task Scheduler)
  - Removed `<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.*" />` (TSK-07: Phase 2 already migrated off `Host.UseWindowsService`)

## Verification

- `findstr` confirms `<OutputType>WinExe</OutputType>` present
- `findstr` confirms `Microsoft.Extensions.Hosting.WindowsServices` absent
- `dotnet restore src/FlaUI.Mcp/FlaUI.Mcp.csproj` succeeded
- `obj/project.assets.json` shows `Skoosoft.Windows/8.0.7` (>= 8.0.7 required for `WinTaskSchedulerManager.CreateOnLogon`/`Delete`)
- No other csproj content changed (TargetFramework, UseWindowsForms, all other PackageReferences, CompileInnoSetup Target intact)

## Deviations from Plan

None — plan executed exactly as written.

## Decisions Made

- Kept `Skoosoft.Windows` at `Version="*"` since the floating reference resolved to 8.0.7 (meets the >= 8.0.7 floor required by Plan 02). No need to pin to `[8.0.7,)`.

## Plan 02 Unblocked

- WinExe subsystem in place — Task Scheduler can launch headless without console window flash.
- `Skoosoft.Windows` 8.0.7 available — `WinTaskSchedulerManager.CreateOnLogon` / `Delete` callable.
- Build is intentionally NOT yet green; Plan 02 replaces the raw schtasks block.

## Self-Check: PASSED

- FOUND: src/FlaUI.Mcp/FlaUI.Mcp.csproj (modified, contains WinExe, no WindowsServices)
- FOUND: Skoosoft.Windows/8.0.7 in obj/project.assets.json
