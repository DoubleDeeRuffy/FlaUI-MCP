# Roadmap: FlaUI-MCP Production Hardening

## Overview

This milestone adds production-grade logging and Windows Service support to the existing FlaUI-MCP server. Phase 1 builds the complete NLog infrastructure so the server has structured, observable diagnostics. Phase 2 wires Windows Service installation, CLI flags, and startup sequencing on top of that foundation so the server can run unattended as a managed background service.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- **Phase 1: Logging Infrastructure** - Programmatic NLog setup with file/console targets, archiving, and ASP.NET Core integration
- **Phase 2: Service Hardening** - CLI flags, Windows Service install/uninstall, firewall rule, startup sequence, and crash safety
- **Phase 3: Task Scheduler Startup** - Convert from Windows Service to Task Scheduler LogonTrigger pattern for desktop session access, WinExe subsystem, AttachConsole, and Debugger.IsAttached guard
- **Phase 4: Streamable HTTP transport** - Implement MCP spec 2025-03-26 Streamable HTTP transport (single `/mcp` endpoint with optional SSE upgrade) alongside the existing legacy SSE transport

## Phase Details

### Phase 1: Logging Infrastructure
**Goal**: The server has structured, observable diagnostics via NLog with proper targets, archive rotation, and framework noise suppression
**Depends on**: Nothing (first phase)
**Requirements**: LOG-01, LOG-02, LOG-03, LOG-04, LOG-05, LOG-06, LOG-07, LOG-08, LOG-09, LOG-10, LOG-11, LOG-12
**Success Criteria** (what must be TRUE):
  1. Running the server produces an Error.log file in {AppBaseDirectory}\Log containing structured entries with longdate, level, callsite, message, and exception fields
  2. Running the server with `-debug` produces a Debug.log file; without `-debug` no Debug.log is created
  3. On startup, any existing .log files from the previous session are zipped into a timestamped archive, and archives beyond the 10-file limit are deleted
  4. Log output contains no System.* or Microsoft.* entries below Warn level
  5. All log write calls are async and LogManager.Shutdown() is called on application exit
Plans:
- 1-01-PLAN.md — NLog packages, LogArchiver (archive/rotation), LoggingConfig (programmatic targets)
- 1-02-PLAN.md — Wire logging into Program.cs, McpServer, SseTransport; -debug flag; ASP.NET Core integration

### Phase 2: Service Hardening
**Goal**: The server installs and runs as a managed Windows Service with full CLI control, firewall rule creation, port-conflict prevention, and crash logging
**Depends on**: Phase 1
**Requirements**: SVC-01, SVC-02, SVC-03, SVC-04, SVC-05, SVC-06, SVC-07, SVC-08, SVC-09, SVC-10, SVC-11
**Success Criteria** (what must be TRUE):
  1. Running `FlaUI-MCP.exe -install` registers a Windows Service named FlaUI-MCP and creates a firewall rule; `-uninstall` removes both; both exit with code 0 without starting the server
  2. Running with `-silent` completes install or uninstall without any user prompts
  3. Running with `-console` stops any already-running FlaUI-MCP service first, then starts the server in the console without port conflicts
  4. An unhandled exception causes a log entry to appear in Error.log before the process terminates
  5. The startup sequence executes in the defined order: CleanOldLogfiles, ConfigureLogging, Firewall, StopRunning, Install/Uninstall/Run
Plans:
- 2-01-PLAN.md — NuGet packages + unified CLI argument parsing + default transport change
- 2-02-PLAN.md — Complete service lifecycle: startup sequence, firewall, install/uninstall, crash handler

### Phase 3: Task Scheduler Startup
**Goal**: The server registers as a Task Scheduler LogonTrigger task (user desktop session) instead of a Windows Service (Session 0), enabling FlaUI to see and automate desktop windows
**Depends on**: Phase 2
**Requirements**: TSK-01, TSK-02, TSK-03, TSK-04, TSK-05, TSK-06, TSK-07, TSK-08, TSK-09
**Success Criteria** (what must be TRUE):
  1. `OutputType` is `WinExe` in .csproj — no `conhost.exe` allocated when launched headless
  2. `--task` uses `WinTaskSchedulerManager.CreateOnLogon()` from `Skoosoft.Windows` (not raw `schtasks.exe`)
  3. `--removetask` uses `WinTaskSchedulerManager.Delete()` (idempotent — no-op if task absent)
  4. `AttachConsole(ATTACH_PARENT_PROCESS)` is called before any `Console.WriteLine` when `-console`, `-install`, or `-uninstall` is active
  5. `Debugger.IsAttached` auto-enables `-c -d` flags and kills stale FlaUI.Mcp processes (excluding own PID)
  6. ConsoleTarget in NLog is gated behind the `-console` flag (not `transport == "sse"`)
  7. `Microsoft.Extensions.Hosting.WindowsServices` package is removed from .csproj
  8. Console window sizing only executes when a real console is attached (not WinExe headless)
  9. `--help` text reflects Task Scheduler as the primary registration method

**Plans:** 8 plans
Plans:
- [ ] 3-00-PLAN.md — Wave 0 infrastructure: verify.cmd + UAT-CHECKLIST.md
- [ ] 3-01-PLAN.md — csproj: OutputType=WinExe, remove WindowsServices package (TSK-01, TSK-07)
- [ ] 3-02-PLAN.md — Replace schtasks shell-out with WinTaskSchedulerManager.CreateOnLogon/Delete + D-1 auto-migration (TSK-02, TSK-03)
- [ ] 3-03-PLAN.md — AttachConsole P/Invoke + headless-safe console window sizing (TSK-04, TSK-08)
- [ ] 3-04-PLAN.md — Debugger.IsAttached guard with stale-process kill (TSK-05)
- [ ] 3-05-PLAN.md — NLog ConsoleTarget gated on -console flag (TSK-06)
- [ ] 3-06-PLAN.md — Finalize --help text per D-4 layout (TSK-09)
- [ ] 3-07-PLAN.md — Manual UAT execution per UAT-CHECKLIST.md, record results

### Phase 4: Streamable HTTP transport

**Goal:** The server exposes a Streamable HTTP transport per MCP spec 2025-03-26 — a single `/mcp` endpoint accepting POST (JSON-RPC) and GET (optional SSE upgrade) — usable by modern MCP clients in addition to the existing legacy `/sse` + `/messages` transport
**Depends on:** Phase 3
**Requirements**: TBD (to be derived during /gsd:discuss-phase 4)
**Success Criteria** (what must be TRUE — to be refined):
  1. `--transport http` (or similar) selects Streamable HTTP, exposing POST/GET on `/mcp`
  2. Existing `--transport sse` legacy endpoints (`/sse`, `/messages`) continue to work unchanged
  3. A modern MCP client (e.g. Claude Code with `"type": "http"`) can initialize, list tools, and invoke a tool against the running server
  4. Session/lifecycle handling matches the spec (Mcp-Session-Id header, proper status codes, stream resumability if scoped in)
  5. Existing tools (Launch, Snapshot, Click, etc.) work identically across both transports

Plans:
- TBD (run /gsd:plan-phase 4 to break down)
