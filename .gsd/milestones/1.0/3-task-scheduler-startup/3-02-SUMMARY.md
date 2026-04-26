---
phase: 3
plan: 02
subsystem: task-scheduler
tags: [phase-3, wave-2, task-scheduler, skoosoft, d-1-migration]
requires: [3-01]
provides:
  - WinTaskSchedulerManager-based --task / --removetask
  - D-1 legacy service auto-uninstall on --task
  - D-2 aliases (-install/-uninstall route to task code path)
affects:
  - src/FlaUI.Mcp/Program.cs
  - src/FlaUI.Mcp/Mcp/SseTransport.cs
tech_stack_added: []
patterns: [skoosoft-windows-manager, idempotent-delete-before-create, auto-migration-d-1]
key_files_created: []
key_files_modified:
  - src/FlaUI.Mcp/Program.cs
  - src/FlaUI.Mcp/Mcp/SseTransport.cs
decisions:
  - schtasks.exe Process.Start fully removed in favor of WinTaskSchedulerManager (locale-stable)
  - D-1 sequencing enforced — service uninstall MUST precede CreateOnLogon, exit 1 if uninstall fails
  - D-1 reverse rule honored — --removetask never touches the service
  - Defensive Delete-before-CreateOnLogon for idempotency on re-install
metrics:
  tasks: 1
  files_changed: 2
  duration_min: ~5
completed: 2026-04-26
---

# Phase 3 Plan 02: Task Scheduler Startup Summary

Replaced raw `schtasks.exe` shell-out with `Skoosoft.Windows.Manager.WinTaskSchedulerManager` and implemented D-1 auto-migration so users moving from v0.x service to v1.x scheduled task cannot end up with both registered simultaneously.

## What Shipped

- **Program.cs constants block** — added `const string TaskName = "FlaUI-MCP";` alongside `ServiceName`/`FirewallRuleName` (top-level, not buried mid-file).
- **Consolidated `if (task || install)` branch** — D-2 aliases route `-install`/`-i` to the same path as `--task`. Sequence:
  1. `ServiceManager.DoesServiceExist(ServiceName)` → if true, log info-line and `ServiceManager.Uninstall(ServiceName, silent: true)` (D-1).
  2. Defensive `WinTaskSchedulerManager.Delete(TaskName)` swallowed-exception (idempotency for re-install).
  3. `WinTaskSchedulerManager.CreateOnLogon(TaskName, description, exeFilePath, "")` → exit 0 on success, exit 1 with logged error on failure.
- **Consolidated `if (removeTask || uninstall)` branch** — D-1 reverse rule: only `WinTaskSchedulerManager.Delete(TaskName)`, no `ServiceManager.*` calls. Idempotent (Skoosoft Delete is no-op if absent).
- **Old code deleted** — both raw `schtasks.exe` Process.Start blocks (~70 lines) plus the legacy `if (install)` ServiceManager.Install branch and `if (uninstall)` ServiceManager.Uninstall branch are gone.

## Smoke-Check Results (verify.cmd)

| # | Check | Status |
|---|-------|--------|
| 1 | `<OutputType>WinExe</OutputType>` in csproj | PASS |
| 2 | `Microsoft.Extensions.Hosting.WindowsServices` NOT in csproj | PASS |
| 3 | `WinTaskSchedulerManager` in Program.cs | PASS |
| 4 | `AttachConsole` in Program.cs | FAIL (out of scope — TSK-04, owned by Plan 04) |
| 5 | `schtasks` NOT in Program.cs | PASS |

The plan-3-02 owned checks (#3 and #5) both pass. Check #4 is owned by a downstream plan.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Remove orphaned `AddWindowsService()` call in SseTransport.cs**

- **Found during:** Task 1 verification (`dotnet build -c Release`)
- **Issue:** `src/FlaUI.Mcp/Mcp/SseTransport.cs:38` called `builder.Services.AddWindowsService()`, an extension method from `Microsoft.Extensions.Hosting.WindowsServices` — but Plan 01 (csproj cleanup) removed that package, leaving an unresolved CS1061 build error.
- **Fix:** Deleted the single `builder.Services.AddWindowsService();` line. The SSE transport runs as a console/scheduled-task process now (not a Windows Service), so the call was dead code.
- **Files modified:** `src/FlaUI.Mcp/Mcp/SseTransport.cs`
- **Commit:** 310e1e4 (single commit per plan)
- **Why auto-fixed:** Blocked plan verification (build error). Strictly within phase 3 scope (Plan 01 left this dangling — Plan 02 finishes the migration).

## Verification

- `dotnet build src\FlaUI.Mcp\FlaUI.Mcp.csproj -c Release` → 0 errors, 4 pre-existing warnings (unused `silent`/`console` locals, unused fields in SessionManager/McpServer — out of scope).
- Grep confirms: 0 occurrences of `schtasks` in Program.cs; 3 occurrences of `WinTaskSchedulerManager.{CreateOnLogon,Delete}`.
- D-1 sequencing manually verified by code review: `ServiceManager.DoesServiceExist` + `ServiceManager.Uninstall` precede `WinTaskSchedulerManager.CreateOnLogon` inside `if (task || install)` branch.
- D-1 reverse rule manually verified: `if (removeTask || uninstall)` branch contains no `ServiceManager.*` calls.

Manual UAT (task creation on clean machine, double-removetask idempotency) is deferred to Plan 07 per UAT-CHECKLIST.md.

## Self-Check: PASSED

- src/FlaUI.Mcp/Program.cs — modified, build green
- src/FlaUI.Mcp/Mcp/SseTransport.cs — modified, build green
- Commit 310e1e4 — FOUND in `git log`
- 0 `schtasks` occurrences, 3 `WinTaskSchedulerManager.*` occurrences
- D-1 + D-2 + D-1-reverse rules implemented per plan spec
