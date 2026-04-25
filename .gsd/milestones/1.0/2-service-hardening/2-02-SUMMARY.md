---
phase: 2-service-hardening
plan: 02
subsystem: infra
tags: [windows-service, firewall, service-lifecycle, crash-handler, console-sizing]

# Dependency graph
requires:
  - phase: 2-service-hardening
    provides: Skoosoft NuGet packages, CLI flag parsing
  - phase: 1-logging-infrastructure
    provides: NLog logging setup, CleanOldLogfiles, ConfigureLogging
provides:
  - Complete Windows Service lifecycle in Program.cs
  - Firewall rule creation for SSE transport
  - Service install/uninstall with Environment.Exit(0)
  - Crash handler via AppDomain.UnhandledException
  - Console window sizing for interactive mode
  - Stop-running-service before console mode
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: [canonical-startup-sequence, environment-userinteractive-service-detection]

key-files:
  created: []
  modified: [src/FlaUI.Mcp/Program.cs]

key-decisions:
  - "Used Environment.UserInteractive instead of WindowsServiceHelpers.IsWindowsService() to avoid extra NuGet dependency"

patterns-established:
  - "Canonical startup sequence: CLI parse, console sizing, logging, crash handler, firewall, stop service, install/uninstall, run"

requirements-completed: [SVC-01, SVC-02, SVC-03, SVC-06, SVC-07, SVC-08, SVC-09, SVC-10, SVC-11]

# Metrics
duration: 2min
completed: 2026-03-17
---

# Phase 2 Plan 02: Service Lifecycle Summary

**Complete Windows Service lifecycle with firewall rules, crash handler, service stop-before-console, and install/uninstall exit behavior**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-17T20:41:48Z
- **Completed:** 2026-03-17T20:43:28Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- Restructured Program.cs with the canonical startup sequence per windows-service-conventions.md
- Added service install/uninstall via ServiceManager with Environment.Exit(0) after each (SVC-01, SVC-02, SVC-03, SVC-06)
- Added firewall rule creation for SSE transport via FirewallManager (SVC-07)
- Added stop-running-service before console mode via ServiceController (SVC-08)
- Added AppDomain.UnhandledException crash handler for Error.log logging (SVC-09)
- Added console window sizing (180x50) when running interactively (SVC-11)
- Added main exception catch block with logger?.Error before rethrow

## Task Commits

Each task was committed atomically:

1. **Task 1: Restructure Program.cs with complete startup sequence** - `51eebd5` (feat)
2. **Task 2: Verify compilation and validate startup sequence order** - verification only, no file changes

## Files Created/Modified
- `src/FlaUI.Mcp/Program.cs` - Complete service lifecycle: console sizing, crash handler, firewall rule, stop service, install/uninstall, main exception catch

## Decisions Made
- Used `Environment.UserInteractive` instead of `WindowsServiceHelpers.IsWindowsService()` to avoid adding `Microsoft.Extensions.Hosting.WindowsServices` NuGet dependency -- both detect service vs interactive mode

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All 9 service requirements (SVC-01 through SVC-11, minus SVC-04/SVC-05 from Plan 01) are implemented
- Phase 2 is complete -- the server is a production-ready Windows Service with full CLI control

---
*Phase: 2-service-hardening*
*Completed: 2026-03-17*
