# Phase 3: Task Scheduler Startup - Context

**Gathered:** 2026-04-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace Windows Service registration (Phase 2) with a Task Scheduler LogonTrigger task so FlaUI-MCP runs in the user's interactive desktop session — not Session 0 — enabling FlaUI/UIA3 to see and automate desktop windows. Phase covers:

- `OutputType=WinExe` subsystem switch (no console allocated headless)
- `--task` / `--removetask` flags via `Skoosoft.Windows.WinTaskSchedulerManager` (`CreateOnLogon` / `Delete`)
- `AttachConsole(ATTACH_PARENT_PROCESS)` P/Invoke before any console output for `-c`/`-i`/`-u` paths
- `Debugger.IsAttached` auto-enabling `-c -d` and killing stale FlaUI.Mcp processes (excluding own PID)
- NLog `ConsoleTarget` gated behind `-console`, not transport type
- Removal of `Microsoft.Extensions.Hosting.WindowsServices` package
- Console window sizing guarded against headless (no-console) execution
- `--help` rewrite reflecting Task Scheduler as primary registration

</domain>

<decisions>
## Implementation Decisions

### Migration from Phase 2 Windows Service

- **D-1:** Running `--task` on a system that still has the v0.x `FlaUI-MCP` Windows Service installed must auto-detect and uninstall the old service silently before creating the Task Scheduler task. Idempotent — no-op if no service is present. Reuse the existing Skoosoft service-helper code path so this is one symmetric call. Same applies in reverse: `--removetask` only removes the scheduled task; it must not touch the service (the service should already be gone from a prior `--task` call or never have existed).

### CLI Flag Surface

- **D-2:** `-install` / `-i` is repurposed as an alias for `--task`; `-uninstall` / `-u` is repurposed as an alias for `--removetask`. The aliases must produce identical behavior to the long flags — same auto-migration (D-1), same exit codes, same logging. Existing scripts/installers calling `-install` continue to work but now register a Task Scheduler task instead of a Windows Service.

### Task Scheduler Trigger

- **D-3a:** Trigger scope is **any-user logon** (not pinned to the installing user's SID). Multi-user machines get the task firing on whichever user logs in. Pass the appropriate `LogonTrigger` configuration to `WinTaskSchedulerManager.CreateOnLogon()` accordingly — verify Skoosoft API supports "any user" vs requires explicit user; if it requires a user, document that and pick a sensible default.
- **D-3b:** No logon-to-launch delay. Task starts immediately when the logon trigger fires. Whatever `WinTaskSchedulerManager.CreateOnLogon()` provides as default is acceptable; do not artificially add a delay.

### Help Text

- **D-4:** `--help` shows `--task` / `--removetask` as the primary registration method, prominently placed. `-install` / `-uninstall` are listed under a clearly labeled "Aliases" or "Compatibility" section near the bottom — discoverable for users grepping for them, but not promoted. No "deprecated" annotation needed (they aren't deprecated — just aliased).

### Claude's Discretion

- Exact wording of the `--help` output and section labels (planner/executor's call, follow established `--help` style of the existing CLI).
- Whether to print a one-line confirmation message when D-1's auto-uninstall actually fires vs. staying silent. Lean toward a single info-level NLog entry so admins can audit migrations from Error.log.
- Internal naming (private method names, class organization) for the Task Scheduler wrapper layer.
- The exact Task Scheduler task name — default to `FlaUI-MCP` (matches the old service name and PROJECT.md "Service name" decision) unless research surfaces a reason to differ.
- Stale-process kill criteria under `Debugger.IsAttached` (TSK-05) — kill by process name `FlaUI.Mcp` excluding own PID is the literal requirement; tightening (e.g., only on the same machine session, only same-user) is planner's call if needed.

### Folded Todos

None — no pending todos matched this phase.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & Requirements (locked scope)
- `.gsd/milestones/1.0/ROADMAP.md` §"Phase 3: Task Scheduler Startup" — phase goal and 9 success criteria
- `.gsd/milestones/1.0/REQUIREMENTS.md` §"Task Scheduler Startup" — TSK-01 through TSK-09 (the source of truth for what's in scope)
- `.gsd/PROJECT.md` — service name (`FlaUI-MCP`), log directory convention (`{AppBaseDirectory}\Log`), Skoosoft ecosystem constraint

### Project Conventions
- `.gsd/codebase/CONVENTIONS.md` — startup sequence, NLog patterns, programmatic-only NLog config
- `.gsd/codebase/STACK.md` — established package set (Skoosoft.Windows, Skoosoft.ProcessLib, NLog)
- `.gsd/codebase/STRUCTURE.md` — Program.cs entry-point organization

### Prior Phase Context (decisions to carry forward)
- `.gsd/milestones/1.0/2-service-hardening/` — Phase 2 CLI parsing, `-install`/`-uninstall` behavior being repurposed in D-2; auto-uninstall path being reused in D-1
- `.gsd/milestones/1.0/1-logging-infrastructure/` — NLog `ConsoleTarget` is what TSK-06 re-gates behind `-console`

### External APIs
- `Skoosoft.Windows.WinTaskSchedulerManager` — `CreateOnLogon(...)` and `Delete(...)` API surface (TSK-02, TSK-03). Researcher should confirm exact method signatures, supported logon-trigger configurations (any-user vs specific-user, see D-3a), and idempotency guarantees.
- Win32 `AttachConsole` — `kernel32.dll`, parameter `ATTACH_PARENT_PROCESS = -1` (TSK-04). Confirm correct P/Invoke signature and how to detect "no parent console" gracefully.
- `OutputType=WinExe` — MSBuild documentation for subsystem implications on `Console.*` calls when no console is attached (TSK-01, TSK-08).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **Phase 2 `-install`/`-uninstall` plumbing** — already wired through CLI parsing, exit-with-code-0, and `Skoosoft.ServiceHelperLib`. D-2 reuses the same flag-parsing slot; D-1 calls into the existing service-uninstall code path before invoking the new Task Scheduler create.
- **Skoosoft.Windows package** — already a `PackageReference` in `src/FlaUI.Mcp/FlaUI.Mcp.csproj`. `WinTaskSchedulerManager` should be available via that package without adding new dependencies.
- **Skoosoft.ProcessLib** — already referenced; likely the right tool for the TSK-05 stale-process kill (process enumeration + kill, with own-PID exclusion).
- **NLog ConsoleTarget setup** — exists from Phase 1; just needs the gating predicate switched from "transport == sse" to "console flag present" (TSK-06).
- **Startup sequence orchestrator** — Phase 2 established the order (CleanOldLogfiles → ConfigureLogging → Firewall → StopRunning → Install/Uninstall → Run); Phase 3 inserts AttachConsole and Debugger.IsAttached handling at the right points without rewriting the spine.

### Established Patterns

- Programmatic NLog config only (no XML) — convention is locked.
- Single-dash short flags + double-dash long flags coexist (`-d`/`--debug`, `-c`/`--console`, `-i`/`--install`). D-2 follows this pattern: `-i`/`--task` doesn't fit, so the alias mapping is `-i`/`-install` ↔ `--task`. Document clearly in `--help` so the inversion (short flag short-name doesn't match long flag long-name) doesn't confuse users.
- `Environment.Exit(0)` after install/uninstall completes — do NOT continue to WebApp boot. Task Scheduler create/remove must follow the same pattern.

### Integration Points

- `Program.cs` — single entry point; everything routes through it. CLI parsing, AttachConsole gate, Debugger.IsAttached guard, startup sequence, and the install/uninstall/task/removetask exit branches all live here.
- `FlaUI.Mcp.csproj` — `OutputType` change (Exe → WinExe), package removal (`Microsoft.Extensions.Hosting.WindowsServices`).
- Console window sizing call site (Phase 2 SVC-11) — must be guarded with an "is a real console attached?" check (TSK-08); if AttachConsole returned 0/false, skip sizing.

</code_context>

<specifics>
## Specific Ideas

- The auto-migration in D-1 should be observable: emit at least one NLog info-level line saying "Detected legacy FlaUI-MCP Windows Service — uninstalling before creating scheduled task" so future audits of Error.log can see when a machine flipped from service to task. (Suggested — planner can adjust phrasing.)
- `--help` ordering: register methods first (`--task` / `--removetask`), then runtime flags (`-c`, `-d`, `-s`, transport), then aliases section last.

</specifics>

<deferred>
## Deferred Ideas

- **Per-user task installation** — D-3a chose any-user logon. If a future requirement surfaces for "install only for me, not other users", that's a `--task --me-only` flag in a later phase.
- **Configurable logon delay** — D-3b chose no delay. If real-world testing reveals desktop/network-not-ready races, add a `--task --delay <seconds>` flag in a follow-up phase.
- **Scripted upgrade tooling** — auto-migration in D-1 covers the in-process case. A standalone `migrate-to-task.ps1` for fleet rollouts is out of scope here.

### Reviewed Todos (not folded)

None — no pending todos matched this phase scope.

</deferred>

---

*Phase: 3-task-scheduler-startup*
*Context gathered: 2026-04-26*
