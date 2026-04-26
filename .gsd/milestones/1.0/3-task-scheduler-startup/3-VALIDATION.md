---
phase: 3
slug: task-scheduler-startup
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-26
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | none — project has no automated test framework (Phase 2 precedent: manual UAT) |
| **Config file** | none |
| **Quick run command** | `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release` |
| **Full suite command** | `verify.cmd` (Wave 0 deliverable — runs build + 4 static smoke checks) |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Release`
- **After every plan wave:** Run `verify.cmd`
- **Before `/gsd:verify-work`:** All static smoke checks green + manual UAT checklist executed
- **Max feedback latency:** ~30 seconds (build) / ~5 minutes (manual UAT)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 3-00-01 | 00 | 0 | infra | smoke | `verify.cmd` | ❌ W0 | ⬜ pending |
| 3-01-01 | 01 | 1 | TSK-01 | static | `findstr /C:"<OutputType>WinExe</OutputType>" src\FlaUI.Mcp\FlaUI.Mcp.csproj` | ✅ | ⬜ pending |
| 3-01-02 | 01 | 1 | TSK-07 | static | `findstr /C:"Microsoft.Extensions.Hosting.WindowsServices" src\FlaUI.Mcp\FlaUI.Mcp.csproj` (must exit 1) | ✅ | ⬜ pending |
| 3-02-01 | 02 | 1 | TSK-02 | static | `findstr /C:"WinTaskSchedulerManager" /C:"CreateOnLogon" src\FlaUI.Mcp\Program.cs` | ✅ | ⬜ pending |
| 3-02-02 | 02 | 1 | TSK-03 | static | `findstr /C:".Delete(" src\FlaUI.Mcp\Program.cs` | ✅ | ⬜ pending |
| 3-02-03 | 02 | 1 | TSK-02 | static | `findstr /C:"schtasks" src\FlaUI.Mcp\Program.cs` (must exit 1 — raw shell-out removed) | ✅ | ⬜ pending |
| 3-03-01 | 03 | 2 | TSK-04 | static | `findstr /C:"AttachConsole" src\FlaUI.Mcp\Program.cs` | ✅ | ⬜ pending |
| 3-03-02 | 03 | 2 | TSK-08 | static | grep verifies Console.WindowWidth/Height guarded by AttachConsole success | ✅ | ⬜ pending |
| 3-04-01 | 04 | 2 | TSK-05 | static | `findstr /C:"Debugger.IsAttached" src\FlaUI.Mcp\Program.cs` | ✅ | ⬜ pending |
| 3-04-02 | 04 | 2 | TSK-05 | manual | UAT: F5 in VS launches with -c -d, kills stale processes | n/a | ⬜ pending |
| 3-05-01 | 05 | 2 | TSK-06 | static | grep ConsoleTarget gating reads `-console` flag, not transport | ✅ | ⬜ pending |
| 3-06-01 | 06 | 3 | TSK-09 | static | `findstr /C:"Task Scheduler" src\FlaUI.Mcp\Program.cs` (in --help block) | ✅ | ⬜ pending |
| 3-07-01 | 07 | 3 | TSK-01..09 | manual | Full UAT checklist (per UAT-CHECKLIST.md) | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `verify.cmd` — runs `dotnet build -c Release` + 4 grep/findstr static smoke checks against built output (exit 0 only if all green)
- [ ] `.gsd/milestones/1.0/3-task-scheduler-startup/UAT-CHECKLIST.md` — manual UAT steps for TSK-01..09 (10 scenarios mirroring Phase 2 format)
- [ ] No xUnit/NUnit install — out of scope per RESEARCH.md (matches Phase 2 precedent of manual UAT 10/10)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Server runs in user desktop session (not Session 0) | TSK-02 | Session ID only observable at runtime in Task Manager | After `--task` install + logoff/logon: open Task Manager → Details → confirm `FlaUI.Mcp.exe` Session column = user's session, not 0 |
| FlaUI can see desktop windows | Phase goal | Requires real desktop apps running | Launch Notepad → invoke `windows_list_windows` MCP tool → Notepad appears in result |
| AttachConsole displays output to parent cmd.exe | TSK-04 | Requires interactive cmd.exe parent | Open cmd.exe → run `FlaUI.Mcp.exe --help` → confirm help text prints to that cmd window |
| WinExe headless launch allocates no conhost | TSK-01, TSK-08 | Requires Process Explorer inspection | Start via Task Scheduler → Process Explorer → confirm no `conhost.exe` child |
| F5 debugger guard kills stale processes | TSK-05 | Requires VS debugger session | F5 with stale FlaUI.Mcp.exe running → confirm stale PID killed, own PID survives |
| `--removetask` is idempotent | TSK-03 | Requires running it twice | Run `--removetask` twice in a row → both exit 0, no error |
| Task survives reboot via LogonTrigger | TSK-02 | Requires reboot | After `--task`: reboot → log in → confirm port 3020 bound by FlaUI.Mcp |
| ConsoleTarget gated on `-console` flag | TSK-06 | Requires log file inspection | Run as scheduled task (no `-console`) → confirm NLog ConsoleTarget inactive (no Console output attempt) |
| Service-uninstall before CreateOnLogon (auto-migration) | sequencing | Requires legacy service installed | Install legacy service → run `--task` → confirm port 3020 freed before CreateOnLogon binds it |

---

## Validation Sign-Off

- [ ] All tasks have static `findstr` verify OR manual UAT entry OR Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify (build + static smoke covers every wave)
- [ ] Wave 0 covers all MISSING references (`verify.cmd`, `UAT-CHECKLIST.md`)
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s for build + static checks
- [ ] `nyquist_compliant: true` set in frontmatter when Wave 0 ships

**Approval:** pending
