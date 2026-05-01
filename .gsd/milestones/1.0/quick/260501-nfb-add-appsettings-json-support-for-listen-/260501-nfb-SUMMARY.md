---
phase: 260501-nfb
plan: "01"
subsystem: configuration
tags: [config, appsettings, env-vars, kestrel, cli-precedence]
type: quick-task
requires:
  - existing CliOptions.Parse signature (9 CliParserTests as regression bar)
  - Microsoft.AspNetCore.App framework reference (transitively provides Microsoft.Extensions.Configuration.{Json,EnvironmentVariables,Memory})
  - AppContext.BaseDirectory convention (already used by LoggingConfig)
provides:
  - ServerConfig record (POCO for "Server" section)
  - CliOverrides record (null-for-absent CLI flag tracking)
  - ConfigResolver.Resolve pure function — layered config merge
  - CliOptions.ParseOverrides — flag-presence parser
  - Layered config wiring at Program.cs:17 (defaults < file < env < CLI)
  - Updated --help output documenting appsettings.json + FLAUI_MCP_ env prefix + precedence chain
affects:
  - src/FlaUI.Mcp/Program.cs (CLI parsing block + help text)
  - src/FlaUI.Mcp/CliOptions.cs (new ParseOverrides method only — Parse unchanged)
tech-stack:
  added: []  # no new NuGet packages
  patterns:
    - "Pure-function resolver — testable without filesystem or real env vars"
    - "Per-key IConfiguration reads (config[\"Server:Port\"]) instead of GetSection<T>().Get<T>() — sidesteps empty-section trap (Research Gotcha #4) and gives silent fall-through on malformed values (Gotcha #5)"
    - "Test-injectable env layer via IDictionary<string,string?> + AddInMemoryCollection (no real env var mutation)"
    - "with-expression for partial record update (opts = opts with { BindAddress = ..., Port = ... })"
key-files:
  created:
    - src/FlaUI.Mcp/ServerConfig.cs
    - src/FlaUI.Mcp/ConfigResolver.cs
    - tests/FlaUI.Mcp.Tests/ConfigResolverTests.cs
  modified:
    - src/FlaUI.Mcp/CliOptions.cs (added ParseOverrides; existing Parse byte-for-byte unchanged)
    - src/FlaUI.Mcp/Program.cs (CLI parsing block + help text)
decisions:
  - "Used Option A from Research § Gotcha #7: new CliOverrides record + new ParseOverrides method. Zero impact on existing 9 CliParserTests."
  - "Per-key config reads (config[\"Server:BindAddress\"]) — not GetSection<T>().Get<T>() — for clean null semantics and silent malformed-value fallthrough."
  - "Tests inject envOverride as IDictionary<string,string?> with already-translated keys (e.g. \"Server:Port\") to bypass real env vars per CONTEXT test isolation requirement."
  - "Help text uses ASCII < and -> (not Unicode arrows) to keep --help legible under console codepage variations."
metrics:
  completed: 2026-05-01
  duration: ~12 minutes
  tasks: 3
  files-changed: 5  # 3 created + 2 modified
  commit: 779c2cf
---

# Quick Task 260501-nfb: appsettings.json + env layered config for bind/port — Summary

Added optional `appsettings.json` + `FLAUI_MCP_` env-variable layers for the Kestrel bind address and port, with CLI flags retaining strict highest precedence. Pure-function `ConfigResolver` keeps the merge unit-testable without touching the real filesystem or real environment variables.

## What Changed

### New types (`src/FlaUI.Mcp/ServerConfig.cs`)

- `public sealed record ServerConfig(string? BindAddress, int? Port)` — POCO modeling the `Server` section of `appsettings.json` / env vars. Nulls = key absent.
- `public sealed record CliOverrides(string? BindAddress, int? Port)` — what the user actually typed on the CLI. Nulls = flag absent (so CLI presence is detected by `field is not null`, not by value comparison against defaults).

### New resolver (`src/FlaUI.Mcp/ConfigResolver.cs`)

```csharp
public static (string BindAddress, int Port) Resolve(
    CliOptions defaults,
    string? appsettingsPath,
    IDictionary<string, string?>? envOverride,
    CliOverrides cli);
```

- Builds a `ConfigurationBuilder` chain: optional JSON file (when `appsettingsPath` is non-empty) → env layer.
- When `envOverride` is non-null: uses `AddInMemoryCollection(envOverride)` (test path).
- When `envOverride` is null: uses `AddEnvironmentVariables(prefix: "FLAUI_MCP_")` (production path; trailing underscore mandatory per Research Gotcha #1).
- Per-key reads via `config["Server:BindAddress"]` and `int.TryParse(config["Server:Port"])` — never throws on a malformed port; falls through to the next layer silently.
- CLI presence test: `cli.BindAddress is not null`, `cli.Port is int cliPort`.

### New CLI parser (`CliOptions.ParseOverrides`)

Sibling to the existing `Parse(args)`. Walks `args` with the same `for` + `switch` pattern, but only matches `--bind` / `--port` (case-insensitive) and emits `CliOverrides(BindAddress: <string?>, Port: <int?>)` with null for absent flags. Existing `Parse` method, `CliOptions` record shape, and 9 `CliParserTests` are byte-for-byte unchanged.

### Wiring at `Program.cs:17`

```csharp
var opts = FlaUI.Mcp.CliOptions.Parse(args);
var cliOverrides = FlaUI.Mcp.CliOptions.ParseOverrides(args);

var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var (mergedBind, mergedPort) = FlaUI.Mcp.ConfigResolver.Resolve(
    defaults: FlaUI.Mcp.CliOptions.Default,
    appsettingsPath: appsettingsPath,
    envOverride: null,
    cli: cliOverrides);
opts = opts with { BindAddress = mergedBind, Port = mergedPort };

var silent = opts.Silent;
// ... existing local-variable extraction unchanged
```

The `with`-expression rebind happens BEFORE `var port = opts.Port;` so the local `port` used by HTTP transport (`Program.cs:267-269`) and SSE transport (`:273`) sees the merged value, and BEFORE `logger.Info(...)` at line 129 so its `bind=` and `port=` log fields show the merged values.

### Help-text update (`Program.cs:52-56`)

After the existing `--bind` / `--port` lines, added 4 lines:

```
Configuration sources (lowest -> highest precedence):
  defaults  <  appsettings.json (Server section)  <  env (FLAUI_MCP_Server__BindAddress, FLAUI_MCP_Server__Port)  <  --bind/--port
  appsettings.json is read from the directory containing FlaUI.Mcp.exe and is optional.
```

### Tests (`tests/FlaUI.Mcp.Tests/ConfigResolverTests.cs`)

8 `[Fact]` methods covering all CONTEXT-mandated scenarios:

| # | Test | Asserts |
|---|------|---------|
| 1 | `NoFileNoEnvNoCli_YieldsDefaults` | bind=127.0.0.1, port=3020 |
| 2 | `FileOnly_AppliesFileValues` | file `{"Server":{"BindAddress":"10.0.0.5","Port":4040}}` → (10.0.0.5, 4040) |
| 3 | `CliOnly_NoFile_AppliesCliValues` | cli=("0.0.0.0", 5000) → (0.0.0.0, 5000) |
| 4 | `CliBeatsFile_EvenWhenCliMatchesDefault` | file=0.0.0.0 + `--bind 127.0.0.1` → 127.0.0.1 (CLI wins despite matching default) |
| 5 | `EnvBeatsFileButLosesToCli` | file=2000, env=3000, cli=4000 → 4000 |
| 6 | `EnvBeatsFile_NoCli` | file=2000, env=3000, no cli → 3000 |
| 7 | `MalformedFilePort_FallsThroughToDefault` | file `Port="not-a-number"` → 3020 (no exception) |
| 8 | `MissingFile_SilentFallback` | non-existent path → defaults (no exception, `optional:true`) |

All tests use `Path.GetTempPath()` for any file-based scenario and `IDictionary<string, string?>` envOverride with already-translated keys (e.g. `["Server:Port"] = "3000"`) — no `AppContext.BaseDirectory` writes, no `Environment.SetEnvironmentVariable` calls.

## Test Results

```
dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj -c Debug --nologo \
  --filter "FullyQualifiedName~ConfigResolverTests|FullyQualifiedName~CliParserTests"
```

Result: **17/17 passing** (9 existing CliParserTests + 8 new ConfigResolverTests, 116 ms).

Full test suite (`dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj`): **27/27 passing** — no regressions in `HttpTransportTests`, `SseTransportTests`, `OriginMiddlewareTests`, `ToolParityTests`, etc.

Build: **0 errors, 3 warnings** (all pre-existing in unrelated files: `HttpTransport.cs:92` MCP9004 deprecation notice, `McpServer.cs:13` CS0414 unused field, `SessionManager.cs:16` CS0414 unused field — out of scope per executor SCOPE BOUNDARY rule).

## Verification Against Success Criteria

- [x] Build clean (0 errors).
- [x] 9 existing CliParserTests still passing.
- [x] 8 new ConfigResolverTests all passing.
- [x] Defaults preserved: no file + no env + no CLI → (127.0.0.1, 3020).
- [x] File layer applies: file `{"Server":{"BindAddress":"10.0.0.5","Port":4040}}` → (10.0.0.5, 4040).
- [x] CLI beats file with strict-presence semantic: `--bind 127.0.0.1` overrides file `0.0.0.0`.
- [x] Env layer behaves correctly (file=2000, env=3000, cli=4000 → 4000; same without cli → 3000).
- [x] Malformed file value falls through silently to default port 3020.
- [x] Missing file is silent (no exception).
- [x] No new NuGet packages — `FlaUI.Mcp.csproj` `<ItemGroup>` for `<PackageReference>` unchanged.
- [x] `--help` output includes the new precedence line documenting `appsettings.json` and the `FLAUI_MCP_` env prefix.
- [x] Pure-function resolver: tests cover all scenarios via `Path.GetTempPath()` + `AddInMemoryCollection`, no `AppContext.BaseDirectory` writes, no real env-var mutation.

## Deviations from Plan

None of substance. Three minor operational notes:

1. **NuGet feed transient unavailability** — A private TeamCity feed (`teamcity.skoosoft.de_v2`) returned HTTP 401 during initial restore. The needed packages were already cached locally; I temporarily disabled the feed via `dotnet nuget disable source teamcity.skoosoft.de_v2`, completed the build, then re-enabled it via `dotnet nuget enable source teamcity.skoosoft.de_v2`. No project files (e.g. NuGet.config) were modified — change was at the user-level NuGet config, transparent to the repo.
2. **No `.gsd/` files committed** — per orchestrator instructions, only the 5 code files (`src/**`, `tests/**`) are in the code commit `779c2cf`. The orchestrator handles the docs commit separately.
3. **Pre-existing build warnings left untouched** — `HttpTransport.cs:92` (MCP9004 deprecated `EnableLegacySse`), `McpServer.cs:13` and `SessionManager.cs:16` (CS0414 unused fields) are unrelated to this task and remain unchanged per the executor SCOPE BOUNDARY rule. They predate this work.

## Commit

```
779c2cf  1.0-quick-260501-nfb-completed: appsettings.json + env layered config for bind/port
```

Files in commit: `src/FlaUI.Mcp/CliOptions.cs`, `src/FlaUI.Mcp/ConfigResolver.cs` (new), `src/FlaUI.Mcp/Program.cs`, `src/FlaUI.Mcp/ServerConfig.cs` (new), `tests/FlaUI.Mcp.Tests/ConfigResolverTests.cs` (new). 5 files changed, 267 insertions.

## Self-Check

**Files exist:**

- [x] `src/FlaUI.Mcp/ServerConfig.cs` — FOUND
- [x] `src/FlaUI.Mcp/ConfigResolver.cs` — FOUND
- [x] `src/FlaUI.Mcp/CliOptions.cs` — FOUND (modified)
- [x] `src/FlaUI.Mcp/Program.cs` — FOUND (modified)
- [x] `tests/FlaUI.Mcp.Tests/ConfigResolverTests.cs` — FOUND

**Commit exists:**

- [x] `779c2cf` — FOUND in git log

## Self-Check: PASSED
