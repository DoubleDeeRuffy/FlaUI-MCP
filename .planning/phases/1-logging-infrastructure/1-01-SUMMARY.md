---
phase: 1-logging-infrastructure
plan: "01"
subsystem: infra
tags: [nlog, logging, archiving, csharp, dotnet]

# Dependency graph
requires: []
provides:
  - Programmatic NLog configuration with file/console targets and async writes
  - LogArchiver.CleanOldLogfiles() for per-session log archive rotation
  - LoggingConfig.ConfigureLogging() with Error.log (always), Debug.log (conditional), console (conditional)
affects: [2-service-infrastructure]

# Tech tracking
tech-stack:
  added: [NLog 5.*, NLog.Web.AspNetCore 5.*]
  patterns:
    - Programmatic NLog setup via LogManager.Setup().LoadConfiguration fluent API
    - Archive-before-configure startup pattern (CleanOldLogfiles before ConfigureLogging)
    - Console target gated on transport mode (SSE only, disabled in stdio)
    - All file/console targets use .WithAsync() for non-blocking writes
    - Framework noise suppression for System.* and Microsoft.*

key-files:
  created:
    - src/FlaUI.Mcp/Logging/LogArchiver.cs
    - src/FlaUI.Mcp/Logging/LoggingConfig.cs
  modified:
    - src/FlaUI.Mcp/FlaUI.Mcp.csproj

key-decisions:
  - "Log directory is app-local: AppContext.BaseDirectory/Log — portable, no system path dependency"
  - "Console target takes enableConsoleTarget bool parameter, not a transport-type enum — simpler callsite"
  - "Max 10 zip archives retained; oldest deleted beyond limit"

patterns-established:
  - "LogArchiver: move .log files to _archive_temp dir, ZipFile.CreateFromDirectory, delete temp — no NLog built-in archiving"
  - "LoggingConfig: static class with static LogDirectory property and ConfigureLogging method — three-arg signature (debug, logDirectory, enableConsoleTarget)"

requirements-completed: [LOG-01, LOG-04, LOG-05, LOG-06, LOG-09, LOG-10]

# Metrics
duration: 2min
completed: 2026-03-17
---

# Phase 1 Plan 01: NLog Infrastructure (LogArchiver + LoggingConfig) Summary

**Programmatic NLog setup with Error.log (always-on), Debug.log (conditional), console target (SSE-only), async writes, and per-session zip archiving with 10-file rotation**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-17T19:34:28Z
- **Completed:** 2026-03-17T19:36:28Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Added NLog 5.* and NLog.Web.AspNetCore 5.* NuGet packages to project
- Created LogArchiver.cs with CleanOldLogfiles() — zips existing .log files on startup, rotates to max 10 zips
- Created LoggingConfig.cs with ConfigureLogging() — Error.log always, Debug.log when debug=true, console when enableConsoleTarget=true
- All file and console targets use .WithAsync() for non-blocking writes
- Framework noise suppression configured for System.* and Microsoft.*
- File layout includes longdate, level, callsite, message, exception
- Console layout strips FlaUI.Mcp namespace prefix for readable output

## Task Commits

Each task was committed atomically:

1. **Task 1: Add NLog packages and create LogArchiver** - `6d3128c` (feat)
2. **Task 2: Create LoggingConfig with programmatic NLog setup** - `7833455` (feat)

## Files Created/Modified
- `src/FlaUI.Mcp/Logging/LogArchiver.cs` - Static class with CleanOldLogfiles(string logDirectory): archives .log files to timestamped zip, rotates to max 10 zips
- `src/FlaUI.Mcp/Logging/LoggingConfig.cs` - Static class with LogDirectory property and ConfigureLogging(bool debug, string logDirectory, bool enableConsoleTarget): programmatic NLog setup
- `src/FlaUI.Mcp/FlaUI.Mcp.csproj` - Added NLog 5.* and NLog.Web.AspNetCore 5.* package references

## Decisions Made
- Log directory is app-local (`AppContext.BaseDirectory/Log`) — same for both console and service mode, portable, no system path dependency
- `ConfigureLogging` takes `enableConsoleTarget` bool rather than a transport type enum — the caller (Program.cs) can pass `transport == "sse"` directly, keeping the signature simple
- 10-zip rotation consistent with Server/Admin pattern from nlog-conventions.md

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `LogArchiver.CleanOldLogfiles()` and `LoggingConfig.ConfigureLogging()` are ready to be called from Program.cs
- Phase 2 (service infrastructure) can proceed — no blockers
- Program.cs integration (calling these from entry point, replacing Console.Error.WriteLine) is in a subsequent plan

---
*Phase: 1-logging-infrastructure*
*Completed: 2026-03-17*

## Self-Check: PASSED

- FOUND: src/FlaUI.Mcp/Logging/LogArchiver.cs
- FOUND: src/FlaUI.Mcp/Logging/LoggingConfig.cs
- FOUND: .planning/phases/1-logging-infrastructure/1-01-SUMMARY.md
- FOUND: commit 6d3128c (Task 1)
- FOUND: commit 7833455 (Task 2)
