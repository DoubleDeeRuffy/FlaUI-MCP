# Phase 2: Service Hardening - Context

**Gathered:** 2026-03-17
**Status:** Ready for planning

<domain>
## Phase Boundary

The server installs and runs as a managed Windows Service with full CLI control, firewall rule creation, port-conflict prevention, and crash logging. Logging infrastructure (Phase 1) is assumed complete. This phase wires the service lifecycle, CLI flags, and startup sequencing.

</domain>

<decisions>
## Implementation Decisions

### Console window sizing
- Fixed size on launch when running interactively (console mode)
- Set console to a readable width x height (e.g., 120x30) so log output doesn't wrap
- Only applies when running in console mode (`-console`), not as a service

### Firewall rule scope
- Firewall rule opens the configured SSE port (default 8080, or whatever `--port` specifies)
- Stdio transport does not need a firewall rule
- Rule is created during install and when running in console mode

### CLI arg coexistence
- Keep both styles: double-dash for value args (`--transport sse`, `--port 8080`), single-dash for boolean flags (`-install`, `-uninstall`, `-silent`, `-debug`, `-console`)
- SSE is the new default transport (changed from stdio)
- Stdio still available via `--transport stdio`

### Service display name & description
- Service name: `FlaUI-MCP`
- Display name: "FlaUI-MCP"
- Description: "MCP server for Windows desktop automation"

### Claude's Discretion
- Exact console window dimensions
- Arg parsing implementation approach
- Error message wording for failed install/uninstall
- How to detect "running interactively" vs "running as service"

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Conventions
- `~/.claude/knowledge/windows-service-conventions.md` -- Startup order, CLI flags, ServiceManager/FirewallManager usage, install/uninstall exit behavior
- `~/.claude/knowledge/nlog-conventions.md` -- Logging setup order (CleanOldLogfiles before ConfigureLogging), referenced in startup sequence
- `~/.claude/CLAUDE.md` -- Global instructions including service conventions and NLog conventions

### Requirements
- `.planning/REQUIREMENTS.md` -- SVC-01 through SVC-11 define all service requirements

### Existing code
- `src/FlaUI.Mcp/Program.cs` -- Current entry point with arg parsing and transport setup; must be restructured
- `src/FlaUI.Mcp/Setup.iss` -- Inno Setup script already expects `-install -silent` and `-uninstall -silent` flags
- `src/FlaUI.Mcp/FlaUI.Mcp.csproj` -- Project file; needs Skoosoft NuGet package references

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Program.cs`: Existing arg parser for `--transport` and `--port` -- extend with new flags
- `Setup.iss`: Already wired for `-install -silent` / `-uninstall -silent` and service stop/start via `net stop FlaUI-MCP`
- `SseTransport`: Existing SSE transport with configurable port -- firewall rule should match this port

### Established Patterns
- Top-level statements in Program.cs -- will need restructuring for startup sequence
- CancellationTokenSource with Console.CancelKeyPress -- reuse for graceful shutdown
- `sessionManager.Dispose()` in finally block -- extend with LogManager.Shutdown()

### Integration Points
- Program.cs is the sole entry point -- all service lifecycle code integrates here
- Phase 1 logging must be wired into startup sequence (CleanOldLogfiles, ConfigureLogging come first)
- Skoosoft.ServiceHelperLib.ServiceManager for sc create/delete
- Skoosoft.Windows.Manager.FirewallManager for netsh advfirewall rules

</code_context>

<specifics>
## Specific Ideas

- SSE should be the default transport instead of stdio -- more common usage pattern for this server
- Setup.iss already has the correct `-install -silent` / `-uninstall -silent` invocation, so CLI flags must match exactly

</specifics>

<deferred>
## Deferred Ideas

None -- discussion stayed within phase scope

</deferred>

---

*Phase: 2-service-hardening*
*Context gathered: 2026-03-17*
