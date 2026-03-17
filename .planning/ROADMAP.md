# Roadmap: FlaUI-MCP Production Hardening

## Overview

This milestone adds production-grade logging and Windows Service support to the existing FlaUI-MCP server. Phase 1 builds the complete NLog infrastructure so the server has structured, observable diagnostics. Phase 2 wires Windows Service installation, CLI flags, and startup sequencing on top of that foundation so the server can run unattended as a managed background service.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Logging Infrastructure** - Programmatic NLog setup with file/console targets, archiving, and ASP.NET Core integration (completed 2026-03-17)
- [ ] **Phase 2: Service Hardening** - CLI flags, Windows Service install/uninstall, firewall rule, startup sequence, and crash safety

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
**Plans:** 2/2 plans complete
Plans:
- [ ] 1-01-PLAN.md — NLog packages, LogArchiver (archive/rotation), LoggingConfig (programmatic targets)
- [ ] 1-02-PLAN.md — Wire logging into Program.cs, McpServer, SseTransport; -debug flag; ASP.NET Core integration

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
**Plans:** 2 plans
Plans:
- [ ] 2-01-PLAN.md — NuGet packages + unified CLI argument parsing + default transport change
- [ ] 2-02-PLAN.md — Complete service lifecycle: startup sequence, firewall, install/uninstall, crash handler

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Logging Infrastructure | 2/2 | Complete    | 2026-03-17 |
| 2. Service Hardening | 0/2 | Not started | - |
