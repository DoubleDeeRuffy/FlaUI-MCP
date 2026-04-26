---
phase: 3-task-scheduler-startup
plan: 07
type: execute
wave: 7
depends_on: [3-06]
files_modified: [.gsd/milestones/1.0/3-task-scheduler-startup/UAT-RESULTS.md]
autonomous: false
requirements:

  - TSK-01
  - TSK-02
  - TSK-03
  - TSK-04
  - TSK-05
  - TSK-06
  - TSK-07
  - TSK-08
  - TSK-09

must_haves:
  truths:

    - All 10 UAT-CHECKLIST.md scenarios are executed manually with results recorded
    - Each TSK-01..TSK-09 requirement has at least one PASS or documented FAIL with remediation note
    - UAT-RESULTS.md captures the final pass/fail tally and any issues to defer to a follow-up phase
    - Server actually runs in user desktop session (Session 1, not Session 0) after --task + logon
    - FlaUI can list desktop windows via the MCP windows_list_windows tool
  artifacts:

    - [object Object]
  key_links:

    - [object Object]

---

<objective>
Execute the manual UAT checklist authored in Plan 00 and record results in `UAT-RESULTS.md`. This is the human gate — Plans 01-06 produced static-check-green code, but desktop-session visibility, F5 debugger behavior, AttachConsole output rendering, and IOException-under-Task-Scheduler are all observable only at runtime by a human.

Purpose: Phase-merge gate. Phase 2 set the precedent of "manual UAT 10/10 with documented results" — Phase 3 follows. Issues found here may be fixed in-line (small drift from spec), recorded as follow-up phase scope, or block the phase-complete commit.

Output: `.gsd/milestones/1.0/3-task-scheduler-startup/UAT-RESULTS.md` documenting each scenario's outcome and any remediation taken.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/milestones/1.0/3-task-scheduler-startup/3-VALIDATION.md
@.gsd/milestones/1.0/3-task-scheduler-startup/UAT-CHECKLIST.md
@verify.cmd
</context>

<tasks>

<task>
  <name>Task 1: Run verify.cmd one more time, then walk UAT-CHECKLIST.md and record UAT-RESULTS.md</name>
  <files>
    <file>.gsd/milestones/1.0/3-task-scheduler-startup/UAT-RESULTS.md</file>
  </files>
  <action>
  **Pre-flight:**

1. From repo root, run `verify.cmd`. It must exit 0. If it doesn't, fix before proceeding (this means a previous plan regressed; run `git diff` and address).
2. Build a fresh `Release` binary: `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release`. Locate `src/FlaUI.Mcp/bin/Release/net8.0-windows/FlaUI.Mcp.exe`.

**Execution:**
Walk through each of the 10 scenarios in `UAT-CHECKLIST.md`, performing the `Steps` exactly. Record the outcome:

- For Scenario 6 (TSK-05 Debugger guard) and Scenario 2 (TSK-02 Task in user session, requires logoff/logon), note that they require human-only environmental conditions (VS debugger, real logon transition).
- For Scenarios involving the legacy v0.x service (D-1 auto-migration), if no legacy service is present on this dev machine, mark as `N/A — no legacy service to migrate from` and document.

**Create `UAT-RESULTS.md`** at `.gsd/milestones/1.0/3-task-scheduler-startup/UAT-RESULTS.md` with this structure:

```markdown

# Phase 3 — UAT Results

**Executed:** <YYYY-MM-DD>
**Build:** Release, FlaUI.Mcp.exe SHA-256 <hash>
**Pre-flight verify.cmd:** PASS

## Scenario Results

| # | Scenario | TSK | Status | Notes |
|---|----------|-----|--------|-------|
| 1 | WinExe headless (no conhost) | TSK-01 | PASS/FAIL/N/A | <observation> |
| 2 | Task registers in user session | TSK-02 | ... | ... |
... (all 10 scenarios) ...

## Issues Found

<bulleted list — empty if all green>

## Remediation

<if issues found: in-line fixes applied, follow-up phase items, or blocking>

## Sign-off

- All 9 TSK requirements verified: <yes/no>
- Phase 3 ready to merge: <yes/no>

```

**Rules for handling failures:**

- **Cosmetic drift** (e.g., help text wording slightly off, log message phrasing): fix in-line in this task, re-run verify.cmd, re-test the affected scenario, mark PASS with note `Fixed inline: <what>`.
- **Behavioral failure** (e.g., IOException at Task Scheduler launch, FlaUI cannot see desktop windows): this is a real bug. STOP UAT execution and emit a `## CHECKPOINT REACHED` block in your final response describing the failure and asking the user how to proceed (in-line fix vs. follow-up phase vs. revert plan).
- **Environmental N/A** (no legacy service to migrate, can't reboot the dev machine): mark `N/A` with explicit reason; this is acceptable as long as the reason is documented.

**Cleanup:**

- Run `--removetask` at the end of UAT to leave the dev machine in a clean state. Document this in the final scenario notes.

**Commit guidance:** Commit `UAT-RESULTS.md` (and any inline fixes) in a single commit titled `docs(3): complete UAT — N/10 scenarios passed`.
  </action>
  <verify>
  <verify>
  <automated>findstr /C:"TSK-01" /C:"TSK-02" /C:"TSK-03" /C:"TSK-04" /C:"TSK-05" /C:"TSK-06" /C:"TSK-07" /C:"TSK-08" /C:"TSK-09" .gsd\milestones\1.0\3-task-scheduler-startup\UAT-RESULTS.md && findstr /C:"Sign-off" .gsd\milestones\1.0\3-task-scheduler-startup\UAT-RESULTS.md</automated>
</verify>

All 9 TSK IDs cross-referenced. Sign-off section present.
  </verify>
  <done>UAT-RESULTS.md exists with all 10 scenarios recorded. All 9 TSK-* requirement IDs traced to at least one PASS / FAIL / N/A entry. Sign-off section present indicating phase-merge readiness.</done>
</task>

</tasks>

<verification>
UAT-RESULTS.md is the verification artifact. Phase 3 is complete when all 9 TSK-* requirements have a PASS or documented N/A in UAT-RESULTS.md, OR a FAIL with explicit remediation path.
</verification>

<success_criteria>

- [ ] All 10 UAT scenarios executed with documented outcomes
- [ ] All 9 TSK requirements traced to results
- [ ] verify.cmd green throughout
- [ ] Dev machine cleaned up (--removetask at end)
- [ ] Sign-off recorded

</success_criteria>

<output>
SUMMARY records: UAT executed against Release build; N/10 scenarios PASS, M N/A with rationale, K FAIL with remediation. Phase 3 sign-off recorded in UAT-RESULTS.md.
</output>
