# Requirements: FlaUI-MCP Production Hardening

**Defined:** 2026-03-17
**Core Value:** Reliable Windows desktop automation via MCP with full observability and unattended service operation

## v1 Requirements

### Logging

- [ ] **LOG-01**: NLog configured programmatically (no XML config files)
- [ ] **LOG-02**: Error.log always active at Error level
- [ ] **LOG-03**: Debug.log active only when `-debug`/`-d` flag is set
- [ ] **LOG-04**: All file targets use async writes
- [ ] **LOG-05**: Standard file layout with longdate, level, callsite, message, exception
- [ ] **LOG-06**: Console layout with time and namespace stripping
- [ ] **LOG-07**: Framework noise suppressed (System.*, Microsoft.* to Warn)
- [ ] **LOG-08**: ASP.NET Core integrated via ClearProviders + UseNLog
- [ ] **LOG-09**: Log archive on startup — zip previous .log files with timestamp
- [ ] **LOG-10**: Archive rotation — keep max 10 zips, delete oldest
- [ ] **LOG-11**: Static logger per class pattern
- [ ] **LOG-12**: LogManager.Shutdown() in finally block

### Service

- [ ] **SVC-01**: CLI flag `-install`/`-i` installs as Windows Service via Skoosoft.ServiceHelperLib
- [ ] **SVC-02**: CLI flag `-uninstall`/`-u` uninstalls Windows Service
- [ ] **SVC-03**: CLI flag `-silent`/`-s` suppresses user prompts during install/uninstall
- [ ] **SVC-04**: CLI flag `-debug`/`-d` enables debug-level logging
- [ ] **SVC-05**: CLI flag `-console`/`-c` runs in console mode
- [ ] **SVC-06**: Install/Uninstall calls Environment.Exit(0) — does not continue to WebApp
- [ ] **SVC-07**: Firewall rule created via Skoosoft.Windows.Manager if not existing
- [ ] **SVC-08**: Stop running service before console mode to avoid port conflicts
- [ ] **SVC-09**: Unhandled exception handler on AppDomain logs before crash
- [ ] **SVC-10**: Proper startup sequence per convention
- [ ] **SVC-11**: Console window sizing when running interactively

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
| LOG-01 | — | Pending |
| LOG-02 | — | Pending |
| LOG-03 | — | Pending |
| LOG-04 | — | Pending |
| LOG-05 | — | Pending |
| LOG-06 | — | Pending |
| LOG-07 | — | Pending |
| LOG-08 | — | Pending |
| LOG-09 | — | Pending |
| LOG-10 | — | Pending |
| LOG-11 | — | Pending |
| LOG-12 | — | Pending |
| SVC-01 | — | Pending |
| SVC-02 | — | Pending |
| SVC-03 | — | Pending |
| SVC-04 | — | Pending |
| SVC-05 | — | Pending |
| SVC-06 | — | Pending |
| SVC-07 | — | Pending |
| SVC-08 | — | Pending |
| SVC-09 | — | Pending |
| SVC-10 | — | Pending |
| SVC-11 | — | Pending |

**Coverage:**
- v1 requirements: 23 total
- Mapped to phases: 0
- Unmapped: 23 ⚠️

---
*Requirements defined: 2026-03-17*
*Last updated: 2026-03-17 after initial definition*
