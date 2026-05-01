---
phase: 260501-nfb
plan: "01"
type: execute
wave: 1
depends_on: []
files_modified:
  - src/FlaUI.Mcp/CliOptions.cs
  - src/FlaUI.Mcp/ServerConfig.cs
  - src/FlaUI.Mcp/ConfigResolver.cs
  - src/FlaUI.Mcp/Program.cs
  - tests/FlaUI.Mcp.Tests/ConfigResolverTests.cs
autonomous: true
requirements:
  - 260501-nfb
must_haves:
  truths:
    - "When no appsettings.json exists and no CLI flags are passed, server binds to 127.0.0.1:3020 (defaults preserved)."
    - "When appsettings.json next to FlaUI.Mcp.exe sets Server.BindAddress and Server.Port, those values reach Kestrel (file layer applied)."
    - "When --bind/--port flags are passed on the CLI, they override appsettings.json and env vars (CLI wins, including --bind 127.0.0.1 vs file 0.0.0.0)."
    - "When FLAUI_MCP_Server__BindAddress / FLAUI_MCP_Server__Port env vars are set, they override appsettings.json but lose to CLI flags."
    - "Existing 9 CliParserTests still pass unchanged (no regression in CLI parsing surface)."
    - "ConfigResolver.Resolve is a pure function: tests verify all precedence scenarios without real filesystem next to the exe and without setting real environment variables."
    - "When appsettings.json is missing or has malformed Server.Port, server falls back silently to the next layer (no crash, no noise)."
    - "--help output documents appsettings.json + FLAUI_MCP_ env prefix and the precedence chain."
  artifacts:
    - path: src/FlaUI.Mcp/ServerConfig.cs
      provides: "ServerConfig POCO bound from appsettings.json Server section + CliOverrides record holding only flags actually passed on the CLI"
      contains: 'record (ServerConfig|CliOverrides)'
    - path: src/FlaUI.Mcp/ConfigResolver.cs
      provides: "Pure-function Resolve(defaults, appsettingsPath, envOverride, cli) layering defaults < file < env < CLI; returns (string BindAddress, int Port)"
      contains: 'static class ConfigResolver'
    - path: src/FlaUI.Mcp/CliOptions.cs
      provides: "Existing CliOptions.Parse unchanged; new ParseOverrides(args) returns CliOverrides with null-for-absent semantics so flag presence is detectable"
      contains: 'ParseOverrides'
    - path: src/FlaUI.Mcp/Program.cs
      provides: "Wiring: CliOptions.Parse + ParseOverrides, appsettingsPath via Path.Combine(AppContext.BaseDirectory, \"appsettings.json\"), ConfigResolver.Resolve invocation, opts rebound via with-expression. Help text at the bind/port section documents appsettings.json + env prefix + precedence."
      contains: 'ConfigResolver\.Resolve'
    - path: tests/FlaUI.Mcp.Tests/ConfigResolverTests.cs
      provides: "xUnit tests covering: defaults / file-only / CLI-only / CLI-beats-file (incl. --bind 127.0.0.1 vs file 0.0.0.0) / env-beats-file-loses-to-CLI / env-beats-file-no-CLI / malformed-port-fallthrough / missing-file-silent-fallback"
      contains: 'ConfigResolverTests'
  key_links:
    - from: "src/FlaUI.Mcp/Program.cs"
      to: "src/FlaUI.Mcp/ConfigResolver.cs"
      via: "Program.cs invokes ConfigResolver.Resolve with defaults=CliOptions.Default, appsettingsPath=Path.Combine(AppContext.BaseDirectory, \"appsettings.json\"), envOverride=null, cli=ParseOverrides(args), then `opts = opts with { BindAddress = bind, Port = port }` so logger.Info on line 121 and HTTP/SSE call sites at Program.cs:259-265 see merged values"
      pattern: 'ConfigResolver\.Resolve'
    - from: "src/FlaUI.Mcp/ConfigResolver.cs"
      to: "Microsoft.Extensions.Configuration"
      via: "ConfigurationBuilder().AddJsonFile(path, optional:true, reloadOnChange:false).AddEnvironmentVariables(prefix:\"FLAUI_MCP_\") in production path; AddInMemoryCollection in test path. No new NuGet packages — APIs come transitively from existing Microsoft.AspNetCore.App framework reference."
      pattern: 'AddJsonFile|AddEnvironmentVariables|FLAUI_MCP_'
    - from: "src/FlaUI.Mcp/CliOptions.cs"
      to: "src/FlaUI.Mcp/ServerConfig.cs (CliOverrides)"
      via: "Static method ParseOverrides(string[] args) walks args looking for --bind / --port and emits CliOverrides(BindAddress: <string?>, Port: <int?>) where null means the flag was absent. Existing Parse(args) signature, return type, and 9 tests stay unchanged."
      pattern: 'ParseOverrides|CliOverrides'
    - from: "tests/FlaUI.Mcp.Tests/ConfigResolverTests.cs"
      to: "src/FlaUI.Mcp/ConfigResolver.cs"
      via: "Tests pass appsettingsPath=null OR a temp-dir path written via File.WriteAllText into Path.GetTempPath(); tests pass envOverride as IDictionary<string,string?> with already-translated keys (e.g. \"Server:Port\") to bypass real env vars; tests assert returned tuple equals expected (bind, port)"
      pattern: 'ConfigResolver\.Resolve'
---

<objective>
Add layered configuration support for the Kestrel bind address and port so operators can set them via an optional `appsettings.json` next to `FlaUI.Mcp.exe` or via `FLAUI_MCP_*` environment variables, while CLI flags continue to take highest precedence.

**Why:** Today `--bind` and `--port` are CLI-only. Operators running FlaUI-MCP via Task Scheduler or as a Windows-managed deployment cannot easily ship environment-specific bind/port without rewriting the task command line. Adding a file + env layer (with CLI still winning) gives them a config-as-data path without changing existing CLI behavior or defaults.

**What this plan does:**
- Introduces a small `ServerConfig` POCO and a `CliOverrides` record (`src/FlaUI.Mcp/ServerConfig.cs`) — types modeling the file/env layer and the CLI-flag-presence layer respectively.
- Adds a `ConfigResolver` static class (`src/FlaUI.Mcp/ConfigResolver.cs`) — pure function layering `defaults < file < env < CLI` and returning the merged `(BindAddress, Port)` tuple.
- Extends `CliOptions` with a sibling `ParseOverrides(args)` parser that returns only the flags actually present on the CLI (null = absent) — leaving the existing `Parse` and 9 existing tests untouched.
- Wires the merge into `Program.cs` at the existing `CliOptions.Parse` call site (line 17) and updates the `--help` block at lines 52–53 to document the new layers and precedence chain.
- Adds `ConfigResolverTests.cs` covering all five mandated scenarios plus malformed-port and missing-file edge cases.

**Output:** Five files (3 new, 2 modified). Build clean, 17 passing tests in `FlaUI.Mcp.Tests` (9 existing + 8 new), `--help` shows new precedence line. No new NuGet packages — all `Microsoft.Extensions.Configuration*` APIs come transitively from the existing `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.
</objective>

<execution_context>
@$HOME/.claude-account2/get-shit-done/workflows/execute-plan.md
@$HOME/.claude-account2/get-shit-done/templates/summary.md
</execution_context>

<context>
@.gsd/STATE.md
@.gsd/milestones/1.0/quick/260501-nfb-add-appsettings-json-support-for-listen-/260501-nfb-CONTEXT.md
@.gsd/milestones/1.0/quick/260501-nfb-add-appsettings-json-support-for-listen-/260501-nfb-RESEARCH.md
@src/FlaUI.Mcp/CliOptions.cs
@src/FlaUI.Mcp/Program.cs
@src/FlaUI.Mcp/FlaUI.Mcp.csproj
@src/FlaUI.Mcp/Logging/LoggingConfig.cs
@tests/FlaUI.Mcp.Tests/CliParserTests.cs

<interfaces>
## Interfaces the executor must use directly

Do NOT explore the codebase to derive these — use them as given.

### Existing — `src/FlaUI.Mcp/CliOptions.cs`

```csharp
namespace FlaUI.Mcp;

public sealed record CliOptions(
    bool Silent, bool Debug, bool Install, bool Uninstall,
    bool Console, bool Task, bool RemoveTask, bool Help,
    string Transport, int Port, string BindAddress)
{
    public static CliOptions Default => new CliOptions(
        Silent: false, Debug: false, Install: false, Uninstall: false,
        Console: false, Task: false, RemoveTask: false, Help: false,
        Transport: "http", Port: 3020, BindAddress: "127.0.0.1");

    public static CliOptions Parse(string[] args) { /* existing — unchanged */ }
}
```

The existing `Parse` walks `args` with a `for` loop + `switch` matching `--bind <addr>` and `--port <number>`. The new `ParseOverrides` MUST mirror that loop (same casing rules: `args[i].ToLowerInvariant()`; same bounds check `i + 1 < args.Length`; same `int.TryParse` for port).

### New — `src/FlaUI.Mcp/ServerConfig.cs` (this plan creates)

```csharp
namespace FlaUI.Mcp;

/// <summary>POCO bound from "Server" section of appsettings.json / env vars. Nulls = key absent at every layer.</summary>
public sealed record ServerConfig(string? BindAddress, int? Port);

/// <summary>What the user actually typed on the CLI. Nulls = flag absent.</summary>
public sealed record CliOverrides(string? BindAddress, int? Port);
```

### New — `src/FlaUI.Mcp/ConfigResolver.cs` (this plan creates)

```csharp
namespace FlaUI.Mcp;

public static class ConfigResolver
{
    public static (string BindAddress, int Port) Resolve(
        CliOptions defaults,
        string? appsettingsPath,
        IDictionary<string, string?>? envOverride,
        CliOverrides cli);
}
```

Contract: `appsettingsPath=null` skips the file layer; `envOverride=null` reads real env vars with prefix `"FLAUI_MCP_"` (trailing underscore mandatory per Research Gotcha #1); a non-null `envOverride` dict bypasses real env and is added via `AddInMemoryCollection` with already-translated keys (e.g. `"Server:Port"`). CLI presence detected by `cli.X is not null`. Malformed `Server:Port` (non-numeric) falls through to defaults silently via `int.TryParse`.

### Existing — `src/FlaUI.Mcp/Program.cs:17` (call site to extend)

```csharp
var opts = FlaUI.Mcp.CliOptions.Parse(args);
```

Must become (per Research § "Production wiring"):

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
```

Downstream call sites (`logger.Info` at line 121; HTTP transport at lines 259–261; SSE at line 265) MUST stay byte-for-byte unchanged — they already read `opts.BindAddress` and `port` (which is `opts.Port` reassigned at line 26). After the merge, the existing `var port = opts.Port;` at line 26 must come AFTER the merge so the local `port` variable used by HTTP/SSE transports sees the merged value.

### Existing — `src/FlaUI.Mcp/Program.cs:52-53` (help text to extend)

Replace the existing `--bind` / `--port` lines with the block from Research § "Help-text update" — four extra `Console.WriteLine` calls after the existing `--port` line announcing the precedence chain and the appsettings.json convention.

### Existing — `tests/FlaUI.Mcp.Tests/CliParserTests.cs`

Style baseline: xUnit `[Fact]`, `using FlaUI.Mcp;`, no fixtures, single `Assert.Equal` per test. New `ConfigResolverTests.cs` MUST live in the same project (`tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj`) and use the same style.

### Framework APIs (transitively available — no `<PackageReference>` to add)

- `Microsoft.Extensions.Configuration.ConfigurationBuilder` + `.Build()`
- `Microsoft.Extensions.Configuration.Json.JsonConfigurationExtensions.AddJsonFile(path, optional:true, reloadOnChange:false)`
- `Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesExtensions.AddEnvironmentVariables(prefix:"FLAUI_MCP_")` — **trailing underscore mandatory**
- `Microsoft.Extensions.Configuration.MemoryConfigurationBuilderExtensions.AddInMemoryCollection(IEnumerable<KeyValuePair<string,string?>>)`
- `IConfiguration` indexer: `config["Server:BindAddress"]` returns `string?`
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Add ServerConfig + CliOverrides types, ConfigResolver, and CliOptions.ParseOverrides</name>
  <files>src/FlaUI.Mcp/ServerConfig.cs, src/FlaUI.Mcp/ConfigResolver.cs, src/FlaUI.Mcp/CliOptions.cs</files>
  <action>
Create the type contracts and the pure-function resolver, then extend `CliOptions` with a sibling parser that reports CLI flag presence.

**1a. Create `src/FlaUI.Mcp/ServerConfig.cs`:**

```csharp
namespace FlaUI.Mcp;

/// <summary>POCO bound from the "Server" section of appsettings.json / FLAUI_MCP_ env vars.
/// Each property is null when the corresponding key is absent at every layer.</summary>
public sealed record ServerConfig(string? BindAddress, int? Port);

/// <summary>What the user actually typed on the CLI. Null = flag absent (so CLI presence
/// is `field is not null`, not `field != default`). Used by ConfigResolver to give CLI
/// strict precedence over file + env layers per CONTEXT.md.</summary>
public sealed record CliOverrides(string? BindAddress, int? Port);
```

**1b. Create `src/FlaUI.Mcp/ConfigResolver.cs`** following Research § "Recommended resolver shape" verbatim. Required behaviors:

- Pure function — no `AppContext`, no `Environment.GetEnvironmentVariables`, no Kestrel.
- Builds an `IConfiguration` chain: optional JSON file → env (prefix `"FLAUI_MCP_"` with trailing underscore — Research Gotcha #1).
- When `envOverride` is non-null, use `AddInMemoryCollection(envOverride)` instead of real env vars (test injection point). When null, use `AddEnvironmentVariables(prefix: "FLAUI_MCP_")`.
- When `appsettingsPath` is null or empty string, skip the file provider entirely (do NOT call `AddJsonFile`).
- When `appsettingsPath` is non-null, call `AddJsonFile(appsettingsPath, optional: true, reloadOnChange: false)`. The `optional: true` covers "file does not exist" silently — no `File.Exists` pre-check needed.
- Read keys per-string (`config["Server:BindAddress"]`, `config["Server:Port"]`) — do NOT use `GetSection("Server").Get<ServerConfig>()` (Research Gotcha #4: empty section returns POCO with default-initialized props which can't be distinguished from "key was 0").
- `BindAddress`: precedence is `cli.BindAddress ?? config["Server:BindAddress"] ?? defaults.BindAddress` — null-coalescing handles all three layers.
- `Port`: precedence is `cli.Port` if non-null, else `int.TryParse(config["Server:Port"])` if it parses, else `defaults.Port`. Use `int.TryParse`, never `int.Parse` — per Research Gotcha #5, malformed values (`"abc"`) fall through silently to the next layer.
- Returns `(string BindAddress, int Port)` tuple.
- Add `using Microsoft.Extensions.Configuration;` (and `Microsoft.Extensions.Configuration.Json` / `.EnvironmentVariables` / `.Memory` only if the compiler complains — extension methods discovery may auto-import via the AspNetCore framework reference).

**1c. Extend `src/FlaUI.Mcp/CliOptions.cs`** with a new public static method `ParseOverrides(string[] args)` returning `CliOverrides`. CRITICAL: the existing `Parse(args)` method, the `CliOptions` record shape, and the 9 existing tests in `CliParserTests.cs` MUST remain byte-for-byte unchanged — only ADD code. The new method walks `args` with the same `for` + `switch` pattern as `Parse`, but only matches `--bind`/`--port` (case-insensitive via `args[i].ToLowerInvariant()`), and emits `CliOverrides(BindAddress: <string?>, Port: <int?>)` where each field is null when the corresponding flag was not passed. Use the same `i + 1 < args.Length` bounds check and `int.TryParse` semantics as the existing `Parse` so a malformed `--port abc` results in `Port: null` (not the default). All other flags (`--silent`, `--debug`, `--transport`, etc.) are ignored — they have no override semantics in this task.

**Why three files in one task:** The types, resolver, and CLI-overrides parser form a single contract surface — they reference each other (`ConfigResolver.Resolve` takes `CliOverrides`, returns values that flow back into a `CliOptions with { ... }`). Splitting them would create a phantom intermediate state where the resolver compiles but has no caller signature. Keep them atomic.

**No NuGet additions.** All `Microsoft.Extensions.Configuration*` APIs come transitively from the existing `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in `FlaUI.Mcp.csproj:19`.

Per CONTEXT.md: file is loaded from `Path.Combine(AppContext.BaseDirectory, "appsettings.json")` — but that path is computed by the CALLER (Program.cs in Task 2), not by the resolver. The resolver remains pure.
  </action>
  <verify>
Build succeeds:

```
dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo -v minimal
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. ConfigResolver, ServerConfig, CliOverrides, and ParseOverrides all compile without warnings. Existing CliOptions.Parse and CliOptions.Default symbols still exported (compiler error if the executor accidentally renamed them).

<automated>dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo -v minimal</automated>
  </verify>
  <done>Three new symbols (`ServerConfig`, `CliOverrides`, `ConfigResolver`) plus one new method (`CliOptions.ParseOverrides`) are exported from the `FlaUI.Mcp` namespace; project builds cleanly; existing `CliOptions.Parse` and 9 existing `CliParserTests` are untouched.</done>
</task>

<task type="auto">
  <name>Task 2: Wire ConfigResolver into Program.cs and update --help text</name>
  <files>src/FlaUI.Mcp/Program.cs</files>
  <action>
Insert the merge layer immediately after `var opts = FlaUI.Mcp.CliOptions.Parse(args);` at line 17 and update the help block at lines 52–53.

**2a. Wiring (around line 17):** Replace the existing 11-line block (lines 17–27, where local variables `silent`, `debug`, … `port`, `helpRequested` are pulled off `opts`) with the merge step BEFORE those locals are read:

```csharp
// === 1. Parse CLI flags (extracted to CliOptions for unit-testability) ===
var opts = FlaUI.Mcp.CliOptions.Parse(args);
var cliOverrides = FlaUI.Mcp.CliOptions.ParseOverrides(args);

// === 1a. Layer config: defaults < appsettings.json < FLAUI_MCP_ env < CLI flags ===
// File path anchored to FlaUI.Mcp.exe (matches LoggingConfig.LogDirectory convention),
// not the working directory — survives Task Scheduler launches.
var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var (mergedBind, mergedPort) = FlaUI.Mcp.ConfigResolver.Resolve(
    defaults: FlaUI.Mcp.CliOptions.Default,
    appsettingsPath: appsettingsPath,
    envOverride: null,
    cli: cliOverrides);
opts = opts with { BindAddress = mergedBind, Port = mergedPort };

var silent = opts.Silent;
var debug = opts.Debug;
var install = opts.Install;
var uninstall = opts.Uninstall;
var console = opts.Console;
var task = opts.Task;
var removeTask = opts.RemoveTask;
var transport = opts.Transport;
var port = opts.Port;
var helpRequested = opts.Help;
```

The `opts = opts with { ... }` line MUST come BEFORE `var port = opts.Port;` so the local `port` variable used by HTTP/SSE transports at lines 259–265 sees the merged value, and BEFORE `logger.Info(...)` at line 121 so its `bind=` and `port=` log fields show the merged values.

Do not add `using` directives unless required. `Path.Combine` and `AppContext.BaseDirectory` are in `System.IO` and `System` — both already implicitly included.

**2b. Help text (lines 52–53):** Replace the existing two `--bind` / `--port` `Console.WriteLine` lines with the block from Research § "Help-text update":

```csharp
Console.WriteLine("  --bind <addr>       Kestrel bind address (default: 127.0.0.1; use 0.0.0.0 for LAN)");
Console.WriteLine("  --port <number>     Listen port (default: 3020)");
Console.WriteLine();
Console.WriteLine("Configuration sources (lowest -> highest precedence):");
Console.WriteLine("  defaults  <  appsettings.json (Server section)  <  env (FLAUI_MCP_Server__BindAddress, FLAUI_MCP_Server__Port)  <  --bind/--port");
Console.WriteLine("  appsettings.json is read from the directory containing FlaUI.Mcp.exe and is optional.");
```

Keep the existing `Console.WriteLine();` separator immediately above the `Aliases` block. This adds 4 lines (one blank, one heading, one precedence chain, one path note).

**Do not modify** any code beyond this CLI parsing block and the help-text block. The stale-process kill block (lines 62–85), debugger guard (lines 87–92), CodePages registration (line 95), console window sizing (97–110), logging configuration (112–117), unhandled exception handler (123–127), firewall rule (129+), and HTTP/SSE call sites (259–265) MUST stay byte-for-byte identical. The merge is invisible to them — they just see `opts.BindAddress` / `opts.Port` / local `port` already carrying the merged values.

**ASCII arrow note:** the help text uses `<` and `->` rather than the Unicode `→` because `Program.cs --help` runs in a console that may not have a UTF-8 codepage active under all launch modes. Pure ASCII keeps `--help` legible everywhere. (German umlauts in code/UI strings remain UTF-8 per project CLAUDE.md, but here the strings are pure-ASCII English.)
  </action>
  <verify>
Build succeeds AND `--help` output contains the new precedence line:

```
dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo -v minimal
dotnet run --project src/FlaUI.Mcp/FlaUI.Mcp.csproj -- --help 2>&1 | Select-String -Pattern 'FLAUI_MCP_Server__'
```

Expected: build clean; the grep finds the precedence line containing `FLAUI_MCP_Server__BindAddress`. (PowerShell `Select-String` is fine on Windows; the equivalent on the Bash tool is `dotnet run ... --help | grep FLAUI_MCP_Server__`.)

<automated>dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo -v minimal</automated>
  </verify>
  <done>`Program.cs` calls `ConfigResolver.Resolve` exactly once with the AppContext-anchored appsettings path, rebinds `opts` via a `with` expression, and the `--help` output documents `appsettings.json`, the `FLAUI_MCP_` env prefix, and the precedence chain. All other `Program.cs` blocks unchanged. Project builds without warnings.</done>
</task>

<task type="auto">
  <name>Task 3: Add ConfigResolverTests covering all 7 precedence scenarios</name>
  <files>tests/FlaUI.Mcp.Tests/ConfigResolverTests.cs</files>
  <action>
Create `tests/FlaUI.Mcp.Tests/ConfigResolverTests.cs` mirroring the style of `CliParserTests.cs` (xUnit `[Fact]`, `using FlaUI.Mcp;`, no fixtures, ~3-line bodies). Use `Path.GetTempPath()` + `Guid.NewGuid()` for any file-based test — never write into `AppContext.BaseDirectory` and never set real env vars (per CONTEXT.md test isolation requirement). Use `IDictionary<string, string?>` envOverride with already-translated keys (e.g. `["Server:Port"] = "3000"`) to inject env-layer values without touching `Environment.SetEnvironmentVariable`.

Mandatory test cases (each is a separate `[Fact]`):

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using FlaUI.Mcp;

namespace FlaUI.Mcp.Tests;

/// <summary>
/// Tests for ConfigResolver — verifies precedence: defaults &lt; appsettings.json &lt; FLAUI_MCP_ env &lt; CLI.
/// All tests are pure-function — no real filesystem next to the exe, no real env vars.
/// File-based tests use Path.GetTempPath(); env-based tests use AddInMemoryCollection via envOverride.
/// </summary>
public class ConfigResolverTests
{
    private static string WriteTempAppsettings(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"flaui-mcp-appsettings-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact] // Scenario 1: defaults
    public void NoFileNoEnvNoCli_YieldsDefaults() {
        var (bind, port) = ConfigResolver.Resolve(
            defaults: CliOptions.Default,
            appsettingsPath: null,
            envOverride: new Dictionary<string, string?>(),
            cli: new CliOverrides(BindAddress: null, Port: null));
        Assert.Equal("127.0.0.1", bind);
        Assert.Equal(3020, port);
    }

    [Fact] // Scenario 2: file only
    public void FileOnly_AppliesFileValues() {
        var path = WriteTempAppsettings("""{"Server":{"BindAddress":"10.0.0.5","Port":4040}}""");
        try {
            var (bind, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?>(),
                cli: new CliOverrides(null, null));
            Assert.Equal("10.0.0.5", bind);
            Assert.Equal(4040, port);
        } finally { File.Delete(path); }
    }

    [Fact] // Scenario 3: CLI only, no file
    public void CliOnly_NoFile_AppliesCliValues() {
        var (bind, port) = ConfigResolver.Resolve(
            defaults: CliOptions.Default,
            appsettingsPath: null,
            envOverride: new Dictionary<string, string?>(),
            cli: new CliOverrides(BindAddress: "0.0.0.0", Port: 5000));
        Assert.Equal("0.0.0.0", bind);
        Assert.Equal(5000, port);
    }

    [Fact] // Scenario 4: CLI beats file — CRITICAL CONTEXT requirement
    public void CliBeatsFile_EvenWhenCliMatchesDefault() {
        // --bind 127.0.0.1 (matches default) MUST still beat appsettings.json: 0.0.0.0
        // because precedence is "flag was passed", not "value differs from default".
        var path = WriteTempAppsettings("""{"Server":{"BindAddress":"0.0.0.0","Port":4040}}""");
        try {
            var (bind, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?>(),
                cli: new CliOverrides(BindAddress: "127.0.0.1", Port: null));
            Assert.Equal("127.0.0.1", bind); // CLI wins
            Assert.Equal(4040, port);         // file wins (CLI didn't pass --port)
        } finally { File.Delete(path); }
    }

    [Fact] // Scenario 5: env beats file but loses to CLI
    public void EnvBeatsFileButLosesToCli() {
        var path = WriteTempAppsettings("""{"Server":{"Port":2000}}""");
        try {
            var (_, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?> { ["Server:Port"] = "3000" },
                cli: new CliOverrides(BindAddress: null, Port: 4000));
            Assert.Equal(4000, port); // CLI > env > file
        } finally { File.Delete(path); }
    }

    [Fact] // Scenario 5b: env beats file with no CLI
    public void EnvBeatsFile_NoCli() {
        var path = WriteTempAppsettings("""{"Server":{"Port":2000}}""");
        try {
            var (_, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?> { ["Server:Port"] = "3000" },
                cli: new CliOverrides(null, null));
            Assert.Equal(3000, port);
        } finally { File.Delete(path); }
    }

    [Fact] // Edge: malformed port falls through silently
    public void MalformedFilePort_FallsThroughToDefault() {
        var path = WriteTempAppsettings("""{"Server":{"Port":"not-a-number"}}""");
        try {
            var (_, port) = ConfigResolver.Resolve(
                defaults: CliOptions.Default,
                appsettingsPath: path,
                envOverride: new Dictionary<string, string?>(),
                cli: new CliOverrides(null, null));
            Assert.Equal(3020, port); // falls back to default, no exception
        } finally { File.Delete(path); }
    }

    [Fact] // Edge: missing file silent fallback (optional:true behavior)
    public void MissingFile_SilentFallback() {
        var bogusPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");
        var (bind, port) = ConfigResolver.Resolve(
            defaults: CliOptions.Default,
            appsettingsPath: bogusPath,
            envOverride: new Dictionary<string, string?>(),
            cli: new CliOverrides(null, null));
        Assert.Equal("127.0.0.1", bind);
        Assert.Equal(3020, port);
    }
}
```

Notes for the executor:

- Use C# 11+ raw string literals (`"""..."""`) for embedded JSON if the project's LangVersion supports it; otherwise use double-quote-escaped strings. The csproj targets `net8.0-windows` which supports C# 12 by default.
- Always pass an empty `Dictionary<string, string?>()` (not `null`) for `envOverride` so the resolver takes the test path (`AddInMemoryCollection`) instead of the production path (`AddEnvironmentVariables`). This guarantees zero coupling to the developer's real env vars.
- The temp file `try/finally` cleanup is best-effort — leaks in `%TEMP%` are acceptable per Research § "Test strategy".
- DO NOT touch `CliParserTests.cs` — its 9 existing tests are the regression bar.

Requirement coverage: this single test file satisfies the "5+ scenarios" constraint (it has 8) and the "no real filesystem next to exe / no real env vars" constraint (temp dir + AddInMemoryCollection).
  </action>
  <verify>
All tests pass:

```
dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --nologo -v minimal
```

Expected: existing 9 `CliParserTests` still pass + new 8 `ConfigResolverTests` pass = 17 total passing in the FlaUI.Mcp.Tests assembly. Other test files (`HttpTransportTests`, `SseTransportTests`, etc.) may add their own counts — what matters is that the new file's 8 tests all pass and none of `CliParserTests` regress.

<automated>dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --nologo -v minimal --filter "FullyQualifiedName~ConfigResolverTests|FullyQualifiedName~CliParserTests"</automated>
  </verify>
  <done>`ConfigResolverTests.cs` exists with at least 7 [Fact] methods covering: defaults, file-only, CLI-only, CLI-beats-file (with --bind 127.0.0.1 vs file 0.0.0.0), env-beats-file-loses-to-CLI, env-beats-file-no-CLI, malformed-port-fallthrough, missing-file-silent-fallback. All 8 new tests pass. All 9 existing CliParserTests still pass. No real env vars touched, no files written to AppContext.BaseDirectory.</done>
</task>

</tasks>

<verification>
End-to-end plan verification (run after all 3 tasks complete):

```
dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo -v minimal
dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --nologo -v minimal
```

**Pass criteria:**

1. Build clean — 0 warnings, 0 errors.
2. Existing `CliParserTests` (9 tests) all green — regression bar.
3. New `ConfigResolverTests` (8 tests) all green — feature bar.
4. Other test files (`HttpTransportTests`, `SseTransportTests`, `OriginMiddlewareTests`, `ToolParityTests`) unaffected — they depend only on `CliOptions` shape, which is unchanged.
5. `dotnet run --project src/FlaUI.Mcp/FlaUI.Mcp.csproj -- --help` prints the new precedence line containing `FLAUI_MCP_Server__BindAddress`.

**Spot-checks the executor should run mentally:**

- `Program.cs:121` logger.Info call still uses `opts.BindAddress` and `port` — but those values are now merged. So a log line emitted during a Task Scheduler launch with `appsettings.json: {"Server":{"Port":4040}}` should read `port=4040` (not `port=3020`).
- HTTP transport at `Program.cs:259-261` and SSE at `:265` are untouched — they read `opts.BindAddress` and the local `port` variable, both already updated.
- No new `<PackageReference>` lines added to `FlaUI.Mcp.csproj` — Configuration APIs come transitively from `Microsoft.AspNetCore.App`.
</verification>

<success_criteria>
- Build clean: `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug` reports 0 warnings, 0 errors.
- Tests green: `dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj` shows all 9 existing CliParserTests + 8 new ConfigResolverTests passing (and no regressions in other test files).
- Defaults preserved: with no appsettings.json, no FLAUI_MCP_ env vars, and no CLI flags, the resolved bind is `127.0.0.1` and port is `3020`.
- File layer applies: with `appsettings.json` next to FlaUI.Mcp.exe containing `{"Server":{"BindAddress":"10.0.0.5","Port":4040}}`, ConfigResolver returns `("10.0.0.5", 4040)`.
- CLI beats file (the strict-presence semantic): passing `--bind 127.0.0.1` on the CLI overrides a file value of `0.0.0.0`, even though `127.0.0.1` matches the hardcoded default.
- Env beats file but loses to CLI: with file Port=2000, env `FLAUI_MCP_Server__Port=3000`, and CLI `--port 4000`, resolved port is `4000`; with the same file + env but no CLI flag, resolved port is `3000`.
- Malformed file value falls through silently: with `"Port":"not-a-number"` the resolver returns the default port (3020) without throwing.
- Missing file is silent: passing a non-existent path for `appsettingsPath` produces no exception (relies on `AddJsonFile(..., optional: true, ...)`).
- No new NuGet packages: `FlaUI.Mcp.csproj` `<ItemGroup>` for `<PackageReference>` is unchanged after this task — Configuration APIs come transitively from `Microsoft.AspNetCore.App`.
- `--help` output includes the precedence line documenting `appsettings.json`, the `FLAUI_MCP_` env prefix, and the chain `defaults < appsettings.json < env < CLI`.
- Pure-function resolver: tests verify all scenarios without writing to `AppContext.BaseDirectory` and without calling `Environment.SetEnvironmentVariable` — using temp-dir files and `AddInMemoryCollection` instead.
</success_criteria>

<output>
After completion, the post-execute SUMMARY at `.gsd/milestones/1.0/quick/260501-nfb-add-appsettings-json-support-for-listen-/260501-nfb-SUMMARY.md` will document: (1) the new types `ServerConfig` and `CliOverrides`, (2) the `ConfigResolver.Resolve` API and its precedence semantics, (3) the `CliOptions.ParseOverrides` companion parser, (4) the merge wiring at `Program.cs:17`, (5) the updated `--help` block at `Program.cs:52-53`, (6) the 8 new `ConfigResolverTests`, and (7) confirmation that no new NuGet packages were added and existing 9 `CliParserTests` continue to pass unchanged.
</output>
