# Requirements: FlaUI-MCP Production Hardening

**Defined:** 2026-03-17
**Core Value:** Reliable Windows desktop automation via MCP with full observability and unattended service operation

## v1 Requirements

### Logging

- [x] **LOG-01**: NLog configured programmatically (no XML config files)
- [x] **LOG-02**: Error.log always active at Error level
- [x] **LOG-03**: Debug.log active only when `-debug`/`-d` flag is set
- [x] **LOG-04**: All file targets use async writes
- [x] **LOG-05**: Standard file layout with longdate, level, callsite, message, exception
- [x] **LOG-06**: Console layout with time and namespace stripping
- [x] **LOG-07**: Framework noise suppressed (System.*, Microsoft.* to Warn)
- [x] **LOG-08**: ASP.NET Core integrated via ClearProviders + UseNLog
- [x] **LOG-09**: Log archive on startup — zip previous .log files with timestamp
- [x] **LOG-10**: Archive rotation — keep max 10 zips, delete oldest
- [x] **LOG-11**: Static logger per class pattern
- [x] **LOG-12**: LogManager.Shutdown() in finally block

### Service

- [x] **SVC-01**: CLI flag `-install`/`-i` installs as Windows Service via Skoosoft.ServiceHelperLib
- [x] **SVC-02**: CLI flag `-uninstall`/`-u` uninstalls Windows Service
- [x] **SVC-03**: CLI flag `-silent`/`-s` suppresses user prompts during install/uninstall
- [ ] **SVC-04**: CLI flag `-debug`/`-d` enables debug-level logging
- [ ] **SVC-05**: CLI flag `-console`/`-c` runs in console mode
- [x] **SVC-06**: Install/Uninstall calls Environment.Exit(0) — does not continue to WebApp
- [x] **SVC-07**: Firewall rule created via Skoosoft.Windows.Manager if not existing
- [x] **SVC-08**: Stop running service before console mode to avoid port conflicts
- [x] **SVC-09**: Unhandled exception handler on AppDomain logs before crash
- [x] **SVC-10**: Proper startup sequence per convention
- [x] **SVC-11**: Console window sizing when running interactively

## v2 Requirements

None planned.

## Out of Scope

| Feature | Reason |
|---------|--------|
| NLog.config XML files | Convention requires programmatic config only |
| Per-module debug log files | Not needed for this project's scope |
| Linux/macOS service support | Windows-only project |
| Remote log shipping | Local file logging sufficient |
| Log viewer UI | Out of scope for MCP server |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| LOG-01 | Phase 1 | Complete |
| LOG-02 | Phase 1 | Complete |
| LOG-03 | Phase 1 | Complete |
| LOG-04 | Phase 1 | Complete |
| LOG-05 | Phase 1 | Complete |
| LOG-06 | Phase 1 | Complete |
| LOG-07 | Phase 1 | Complete |
| LOG-08 | Phase 1 | Complete |
| LOG-09 | Phase 1 | Complete |
| LOG-10 | Phase 1 | Complete |
| LOG-11 | Phase 1 | Complete |
| LOG-12 | Phase 1 | Complete |
| SVC-01 | Phase 2 | Complete |
| SVC-02 | Phase 2 | Complete |
| SVC-03 | Phase 2 | Complete |
| SVC-04 | Phase 2 | Pending |
| SVC-05 | Phase 2 | Pending |
| SVC-06 | Phase 2 | Complete |
| SVC-07 | Phase 2 | Complete |
| SVC-08 | Phase 2 | Complete |
| SVC-09 | Phase 2 | Complete |
| SVC-10 | Phase 2 | Complete |
| SVC-11 | Phase 2 | Complete |

**Coverage:**
- v1 requirements: 23 total
- Mapped to phases: 23
- Unmapped: 0

---
*Requirements defined: 2026-03-17*
*Last updated: 2026-03-17 after roadmap creation*
