# Phase 3: Task Scheduler Startup - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-26
**Phase:** 3-task-scheduler-startup
**Areas discussed:** Service-to-task migration, Old flag fate, Trigger scope and timing, Help text rewrite

---

## Service-to-task Migration

**Question:** A user installed v0.x as a Windows Service via `-install`. They update to v1.0 and run `--task`. What should happen?

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-detect + uninstall | Silently uninstall existing service, then create task. Idempotent single-command migration. | ✓ |
| Refuse with guidance | Print "Existing Windows Service detected. Run `-uninstall` first, then `--task`." | |
| Coexist | Install task on top of existing service. User cleans up service manually. | |
| Drop install/uninstall entirely | Assume clean install or user knows `sc.exe delete` themselves. | |

**User's choice:** a — Auto-detect + uninstall.
**Notes:** Idempotent migration; reuse existing Skoosoft service-uninstall code path.

---

## Old Flag Fate (`-install` / `-uninstall`)

**Question:** TSK-07 removes `Microsoft.Extensions.Hosting.WindowsServices`. What happens to the existing service-install flags?

| Option | Description | Selected |
|--------|-------------|----------|
| Repurpose as aliases | `-install` → `--task`, `-uninstall` → `--removetask`. Same flags, new behavior. | ✓ |
| Remove entirely | Only `--task`/`--removetask` exist. Cleanest CLI but breaks scripts. | |
| Soft-deprecate | Forward to new flags with a deprecation warning. | |
| Hard-error | Print "Windows Service support removed in v1.0. Use --task." and exit 1. | |

**User's choice:** a — Repurpose as aliases.
**Notes:** Smoothest upgrade path; existing scripts and installers continue working.

---

## Trigger Scope (3a)

**Question:** Which user(s) trigger the LogonTrigger task?

| Option | Description | Selected |
|--------|-------------|----------|
| Current user only | Pin to invoker SID — only fires when that user logs in. | |
| Any user logon | Fires for whoever logs in. | ✓ |

**User's choice:** b — Any user logon.

## Trigger Timing (3b)

**Question:** Logon-to-launch delay?

| Option | Description | Selected |
|--------|-------------|----------|
| No delay | Start immediately on logon. Skoosoft default likely. | ✓ |
| 30s delay | Let desktop, network, profile settle before FlaUI starts. | |
| Configurable via flag | `--task --delay 30`, default no delay. | |

**User's choice:** a — No delay.

---

## Help Text Rewrite

**Question:** TSK-09 says `--help` reflects Task Scheduler as primary. What about the now-aliased service flags?

| Option | Description | Selected |
|--------|-------------|----------|
| Primary + aliases section | `--task`/`--removetask` primary; `-install`/`-uninstall` listed under Aliases/Compatibility. | ✓ |
| Hide old flags | Aliases work but undocumented. Cleanest help. | |
| Show both equally | List side-by-side as equivalent. | |
| Show with deprecation note | `-install (deprecated, use --task)`. | |

**User's choice:** a — Primary + aliases section.

---

## Claude's Discretion

- Exact `--help` wording and section labels.
- Whether to log a one-line info message when D-1 auto-uninstall actually fires (suggested yes; planner can adjust).
- Task Scheduler task name (default `FlaUI-MCP` to match old service).
- Stale-process kill criteria narrowing under `Debugger.IsAttached` (TSK-05).

## Deferred Ideas

- Per-user-only task install flag — not needed; revisit if requirement surfaces.
- Configurable logon delay — only if real-world testing reveals races.
- Standalone migration tooling for fleet rollouts — out of scope.
