# FlaUI-MCP — Production Hardening

## What This Is

FlaUI-MCP is a Model Context Protocol server that exposes Windows UI Automation as JSON-RPC tools for AI agents (Claude, GitHub Copilot). This milestone adds production-grade logging and Windows Service support so it can run as a managed background service with proper diagnostics.

## Core Value

The MCP server must reliably automate Windows desktop applications via accessibility APIs — logging and service support ensure it runs unattended with full observability.

## Requirements

### Validated

- ✓ MCP server with stdio and SSE transports — existing
- ✓ Tool-based architecture for UI automation (launch, snapshot, click, type, fill, getText, screenshot, batch) — existing
- ✓ Ref-based element interaction via ElementRegistry — existing
- ✓ Session/window management via SessionManager — existing
- ✓ Multi-architecture builds (x64 + ARM64) — existing
- ✓ CLI args for transport and port selection — existing

### Active

- [ ] NLog logging with programmatic configuration (no XML)
- [ ] Error.log always active, Debug.log only with `-debug` flag
- [ ] Log archive on startup (zip previous session, clean rotation)
- [ ] Framework noise suppression (System.*, Microsoft.*)
- [ ] ASP.NET Core NLog integration for SSE transport
- [ ] Command-line flags: `-install`, `-uninstall`, `-silent`, `-debug`, `-console`
- [ ] Windows Service installation/uninstall via Skoosoft.ServiceHelperLib
- [ ] Firewall rule creation via Skoosoft.Windows.Manager
- [ ] Stop running service before console mode (avoid port conflicts)
- [ ] Unhandled exception logging with AppDomain handler
- [ ] Proper startup sequence (CleanOldLogfiles → ConfigureLogging → Firewall → StopRunning → Install/Uninstall → Run)

### Out of Scope

- Linux/macOS support — Windows UI Automation APIs only
- Configuration files — CLI args are sufficient
- Multi-tenancy / authentication — local trust boundary
- Database or external API integrations — not needed

## Context

- Existing codebase uses `Console.Error` for diagnostics — no structured logging
- Project targets `net8.0-windows` with ASP.NET Core for SSE transport
- NuGet packages available: `Skoosoft.ServiceHelperLib`, `Skoosoft.Windows.Manager`
- Conventions follow ConfigHub project patterns documented in `~/.claude/knowledge/`
- Service name: `FlaUI-MCP`
- Log directory: `{AppBaseDirectory}\Log` (portable, next to the exe)

## Constraints

- **Platform**: Windows only (net8.0-windows) — UI Automation requirement
- **NLog config**: Programmatic only, no XML files — convention enforcement
- **Service lib**: Must use Skoosoft.ServiceHelperLib for sc create/delete wrapping
- **Firewall lib**: Must use Skoosoft.Windows.Manager for netsh wrapping

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| App-local log directory | Portable deployment, logs next to exe | — Pending |
| Service name `FlaUI-MCP` | Matches repo name, clear identity | — Pending |
| Skoosoft NuGet packages for service/firewall | Consistent with ConfigHub ecosystem | — Pending |

---
*Last updated: 2026-03-17 after initialization*
