# Quick Task 260501-nfb: Add appsettings.json support for listen IP and Port (CLI takes precedence) - Context

**Gathered:** 2026-05-01
**Status:** Ready for planning

<domain>
## Task Boundary

Add `appsettings.json` support for the Kestrel bind address and port, layered with CLI flag overrides. Today these values are CLI-only (`--bind`, `--port` parsed in `src/FlaUI.Mcp/CliOptions.cs`). After this task:

- Defaults: `127.0.0.1:3020` (unchanged)
- File: optional `appsettings.json` next to the exe with a `Server` section
- Env: `FLAUI_MCP_` prefixed env vars
- CLI: highest precedence, unchanged surface (`--bind`, `--port`)

Out of scope: changing default bind address or port; introducing other config keys; changes to logging/transport/firewall config.

</domain>

<decisions>
## Implementation Decisions

### Config file location
- File path: `Path.Combine(AppContext.BaseDirectory, "appsettings.json")` — same convention as `LoggingConfig.LogDirectory` (`AppContext.BaseDirectory + "Log"`). Works for both Task Scheduler launches and console runs because it is anchored to the binary, not the working directory.
- File is optional. If missing, fall back to defaults silently — no warning, no log noise.
- No environment-specific overlays (`appsettings.Production.json` etc.) — single file only. Keep surface minimal.

### Implementation approach
- Use `Microsoft.Extensions.Configuration` + `Microsoft.Extensions.Configuration.Json` + `Microsoft.Extensions.Configuration.EnvironmentVariables` from the already-referenced `Microsoft.AspNetCore.App` framework reference. No new NuGet packages.
- Build the configuration chain explicitly (no implicit ASP.NET host config), so precedence is auditable from one place.
- Bind to a small `ServerConfig` POCO (`BindAddress`, `Port`) via `configuration.GetSection("Server").Get<ServerConfig>()` or equivalent.

### Env var layer
- Prefix: `FLAUI_MCP_` (single underscore between prefix and key, double underscore for section nesting per `Microsoft.Extensions.Configuration.EnvironmentVariables` convention).
- Examples:
  - `FLAUI_MCP_Server__BindAddress=0.0.0.0`
  - `FLAUI_MCP_Server__Port=4040`

### Precedence (lowest → highest)
1. Hardcoded defaults (`CliOptions.Default`: `127.0.0.1`, `3020`)
2. `appsettings.json` `Server` section (if file present and keys set)
3. Environment variables (`FLAUI_MCP_Server__BindAddress`, `FLAUI_MCP_Server__Port`)
4. CLI flags (`--bind`, `--port`)

CLI always wins. CLI presence is detected by "the flag was passed" — not by "the value differs from default", so passing `--bind 127.0.0.1` explicitly still beats an `appsettings.json` value of `0.0.0.0`.

### Config section shape
```json
{
  "Server": {
    "BindAddress": "127.0.0.1",
    "Port": 3020
  }
}
```

### Code organisation
- New file `src/FlaUI.Mcp/AppSettings.cs` (or similar) holding:
  - `ServerConfig` POCO (BindAddress + Port)
  - A `Load(string baseDirectory)` (or static factory) method returning the configuration result.
- `CliOptions.Parse` keeps its current signature (parses CLI only). A new layer in `Program.cs` (or a small `ConfigResolver`) merges file → env → CLI.
- The merge step must be unit-testable without spinning up Kestrel, mirroring the existing rationale for extracting `CliOptions`.

### Tests
Project: `tests/FlaUI.Mcp.Tests`. Cover four scenarios per the user request:
1. Neither file nor CLI → defaults
2. File only → file values applied
3. CLI only → CLI values applied (no file present)
4. CLI + file → CLI overrides file
Plus add a 5th: env var beats file but loses to CLI (smoke-test the env layer added in this task).

Tests must not depend on the actual filesystem next to the exe — pass `baseDirectory` as a parameter or use a temp dir per test.

### Claude's Discretion
- Exact class/file names (POCO name, resolver name) — any clear, idiomatic naming.
- Whether to merge inline in `Program.cs` or extract a small `ConfigResolver` — prefer extraction for testability, but if a 10-line inline merge is cleaner, that is acceptable as long as it is testable.
- README/help-text updates: update `--help` output in `Program.cs` to mention `appsettings.json` and the env var prefix; do not add a separate README section unless one already exists for config.

</decisions>

<specifics>
## Specific Ideas

- Existing surface to preserve: `--bind <address>` and `--port <number>` CLI flags (`src/FlaUI.Mcp/CliOptions.cs:87-89, 84-86`).
- Existing default record: `CliOptions.Default` (`127.0.0.1`, `3020`) — keep authoritative; remove duplication if config layer needs its own defaults.
- Existing logging info line in `Program.cs:121` already prints `bind` and `port` — should keep working unchanged after the merge step.
- Mirror the existing extraction philosophy from `CliOptions.cs:1-9` ("Pure value record — extracted from Program.cs so transport-default and bind-address parsing can be unit-tested without spinning up Kestrel").
- Test fixture style: existing `CliParserTests.cs` is the parity baseline; new tests should sit alongside it.
- HTTP transport call site uses `opts.BindAddress` and `port` (`Program.cs:259-261`); SSE uses `opts.BindAddress, port` (`Program.cs:265`). The merged values must reach those call sites.

</specifics>

<canonical_refs>
## Canonical References

- `src/FlaUI.Mcp/CliOptions.cs` — current CLI parser to extend / wrap.
- `src/FlaUI.Mcp/Program.cs:17, 121, 259-265` — call sites consuming the resolved options.
- `src/FlaUI.Mcp/Logging/LoggingConfig.cs:15` — existing `AppContext.BaseDirectory` convention.
- `src/FlaUI.Mcp/FlaUI.Mcp.csproj:19` — `Microsoft.AspNetCore.App` framework reference (Configuration packages are transitively available).
- `tests/FlaUI.Mcp.Tests/CliParserTests.cs` — existing test style for option parsing.

</canonical_refs>
