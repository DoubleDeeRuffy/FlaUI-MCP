# Quick Task 260501-nfb: appsettings.json + Env Layer for Bind/Port — Research

**Researched:** 2026-05-01
**Domain:** `Microsoft.Extensions.Configuration` (Json + EnvironmentVariables) layered with existing `CliOptions`
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- File path: `Path.Combine(AppContext.BaseDirectory, "appsettings.json")` (anchored to binary, not CWD).
- File is optional. Missing file → silent fallback to defaults. No warning, no log noise.
- No environment-specific overlays (no `appsettings.Production.json`).
- Use `Microsoft.Extensions.Configuration` + `.Json` + `.EnvironmentVariables` from the existing `Microsoft.AspNetCore.App` framework reference. **No new NuGet packages.**
- Build the configuration chain explicitly (no implicit ASP.NET host config) so precedence is auditable in one place.
- Bind to a small `ServerConfig` POCO (`BindAddress`, `Port`).
- Env prefix: `FLAUI_MCP_`. Double-underscore for nesting (e.g. `FLAUI_MCP_Server__Port`).
- Precedence (lowest → highest): defaults → file → env → CLI.
- CLI wins on **flag-was-passed** (not value-differs-from-default), so `--bind 127.0.0.1` explicitly beats `appsettings.json: 0.0.0.0`.
- `CliOptions.Parse` keeps its current signature. New merge layer (in `Program.cs` or a small `ConfigResolver`) layers file → env → CLI.
- Tests must not depend on the real filesystem next to the exe — pass `baseDirectory` as a parameter or use a temp dir per test.
- 5 test scenarios: defaults / file-only / CLI-only / CLI+file (CLI wins) / env beats file but loses to CLI.

### Claude's Discretion
- Exact class/file names (POCO, resolver) — any clear, idiomatic naming.
- Inline merge in `Program.cs` vs. extracted `ConfigResolver` — prefer extraction for testability; ~10-line inline merge acceptable if testable.
- Help-text update in `Program.cs --help` to mention `appsettings.json` + env prefix. No separate README section unless one already exists.

### Deferred Ideas (OUT OF SCOPE)
- Other config keys beyond `Server:BindAddress` / `Server:Port`.
- Changing default bind address or port.
- Logging/transport/firewall config layering.
- `IOptions<T>` / reload-on-change.
</user_constraints>

## Approach

Add a thin merge layer between `CliOptions.Parse(args)` and `Program.cs` consumption. Keep `CliOptions` untouched in shape (record, public surface, existing tests), but extend it with one piece of data: **which flags the user actually passed**. Pair that with a `ServerConfig` POCO bound from `IConfiguration` (file + env), then merge with CLI-passed flags taking precedence.

```
defaults (CliOptions.Default)
   ↓ overridden by
appsettings.json  ─┐
                   │  built into single IConfiguration via ConfigurationBuilder
env FLAUI_MCP_*  ─┘
   ↓ overridden by
CLI flags (only the ones actually present in args)
```

**Recommendation:** extract a `ConfigResolver.Resolve(...)` static method. It is a 30-line pure function, fully unit-testable, no Kestrel, no real env vars, no AppContext.

## Key APIs

All available transitively via `<FrameworkReference Include="Microsoft.AspNetCore.App" />` — no `<PackageReference>` additions needed.

| API | Namespace | Notes |
|---|---|---|
| `ConfigurationBuilder` | `Microsoft.Extensions.Configuration` | Compose providers explicitly; call `.Build()` for `IConfigurationRoot`. |
| `JsonConfigurationExtensions.AddJsonFile(path, optional, reloadOnChange)` | `Microsoft.Extensions.Configuration.Json` | Pass absolute path. Use `optional: true, reloadOnChange: false`. |
| `EnvironmentVariablesExtensions.AddEnvironmentVariables(prefix)` | `Microsoft.Extensions.Configuration.EnvironmentVariables` | Prefix **with trailing underscore** — see Gotcha #1. |
| `IConfiguration.GetSection("Server").Get<ServerConfig>()` | `Microsoft.Extensions.Configuration.Binder` | Returns `null` if section absent and no keys present; throws `InvalidOperationException` on type-coercion failure. |
| `MemoryConfigurationBuilderExtensions.AddInMemoryCollection(...)` | `Microsoft.Extensions.Configuration` | Use in tests to inject env-like overrides without touching real env vars. |

**Verified versions:** these are part of .NET 8 BCL (shipped with the runtime via `Microsoft.AspNetCore.App` shared framework). No version pinning needed — they are tied to the targeted framework (`net8.0-windows`).

## Gotchas

### 1. Env var prefix: trailing underscore matters

`AddEnvironmentVariables("FLAUI_MCP_")` strips the literal prefix string. The trailing underscore is part of the prefix you strip, not separate punctuation:

| Env var | Pass `prefix:` | Resulting config key |
|---|---|---|
| `FLAUI_MCP_Server__Port=4040` | `"FLAUI_MCP_"` | `Server:Port` ✓ |
| `FLAUI_MCP_Server__Port=4040` | `"FLAUI_MCP"` | `_Server:Port` ✗ (underscore stays) |

**Always pass `"FLAUI_MCP_"` with the trailing underscore.** Source: [MS Learn — Configuration providers, "Prefixes" section](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#prefixes) ("The prefix is stripped off when the configuration key-value pairs are read.").

### 2. Double-underscore → colon translation is unconditional

The provider replaces `__` with `:` in **all** loaded env-var names regardless of whether a prefix is set. So `FLAUI_MCP_Server__BindAddress` → strip prefix → `Server__BindAddress` → translate → `Server:BindAddress`. Documented as platform-portable behavior because POSIX shells reject `:` in env names.

### 3. Case insensitivity

Configuration keys are case-insensitive across all providers (file & env). On Windows, env var names are case-insensitive at the OS level too. So `flaui_mcp_server__port` works the same as `FLAUI_MCP_Server__Port`. Don't add normalization code — the framework handles it.

### 4. `Get<T>()` returns `null` only when the section is fully absent

`configuration.GetSection("Server").Get<ServerConfig>()`:
- File missing AND no env vars set → returns `null` (or default-value POCO depending on overload). Treat null as "no overrides".
- File has `"Server": {}` (empty section) → returns a `ServerConfig` instance with default-initialized props (`BindAddress = null`, `Port = 0`). **Do not blindly use** — check each property for null/zero before treating it as an override.

**Cleaner alternative:** read individual keys directly:
```csharp
string? fileBind = config["Server:BindAddress"];   // null if absent
int? filePort = int.TryParse(config["Server:Port"], out var p) ? p : (int?)null;
```
Returns `null` cleanly when a key is missing at every layer. Sidesteps the Get<T>-with-empty-POCO issue and gives you precise per-key precedence.

### 5. Malformed values: fail fast vs. fall through

CONTEXT.md says "missing file = silent default fallback" but is silent on malformed values (e.g. `Port="abc"`). Two options:

- **(A) Fail fast** — `int.Parse` throws → bubble out → process exits with a clear error. Pro: surfaces config bugs immediately. Con: won't "self-heal" by falling back to defaults.
- **(B) Silent fall-through** — `int.TryParse` returns false → treat as if the key were absent. Pro: matches "missing file = silent fallback" spirit. Con: hides typos in `appsettings.json`.

**Recommendation: (B) silent fall-through, with a single-line `Console.Error` warning.** Rationale: this app boots from Task Scheduler with no console; a thrown exception just disappears. A best-effort warning to stderr (still no-op under WinExe-no-console) plus fall-through to the next layer keeps the server running. Using `int.TryParse` and `IPAddress.TryParse` for the two values gives this for free.

If a future operator wants strict mode, that's a one-line addition later. Don't over-engineer now.

### 6. `Get<T>()` does throw on hard binder failures

For completeness: if you go the `Get<ServerConfig>()` route and `Port` is a non-numeric string, `ConfigurationBinder` throws `InvalidOperationException: Failed to convert configuration value at 'Server:Port' to type 'System.Int32'`. So Gotcha #5 only applies if you take the per-key string-reading path (which I recommend).

### 7. CLI "flag was passed" detection — minimal-impact change

`CliOptions.Parse` currently always returns `BindAddress` set to either the parsed value or the default sentinel `127.0.0.1`. There is no way for a downstream consumer to tell which.

**Three options, ordered by minimal blast radius:**

| Option | Change | Test impact |
|---|---|---|
| **A. Add a `CliOverrides` record alongside `CliOptions`** | New `public sealed record CliOverrides(string? BindAddress, int? Port)` returned by a new `CliOptions.ParseOverrides(args)` (or as a second tuple element from `Parse`). Existing `CliOptions` shape and `Parse` signature unchanged. | Zero impact on existing 9 tests. |
| **B. Add nullable companion fields to `CliOptions`** | `BindAddressOverride`, `PortOverride` as `string?` / `int?`. Existing fields stay for back-compat. | Zero impact on existing tests; record constructor grows. |
| **C. Add boolean flags** | `IsBindAddressFromCli`, `IsPortFromCli`. | Zero impact; less elegant — duplicates state. |

**Recommendation: A.** Cleanest separation: `CliOptions` stays "the resolved CLI view" (with defaults filled), `CliOverrides` is "what the user actually typed". The merge layer takes `CliOverrides` to decide precedence, and `Program.cs` still consumes the final `CliOptions`-shaped tuple. Existing `CliParserTests` keeps testing `CliOptions.Parse` exactly as before.

A minor variant of A that's even simpler: have `Parse` return the existing `CliOptions` PLUS a `HashSet<string> presentFlags` (or a `CliOverrides`) as a second return value via a `(CliOptions, CliOverrides) ParseWithOverrides(args)` method, and keep `Parse(args)` as a thin wrapper returning only `CliOptions` for back-compat with existing tests.

## Recommended resolver shape

```csharp
namespace FlaUI.Mcp;

/// <summary>POCO bound from "Server" section of appsettings.json / env vars.</summary>
public sealed record ServerConfig(string? BindAddress, int? Port);

/// <summary>What the user actually typed on the CLI (null = flag absent).</summary>
public sealed record CliOverrides(string? BindAddress, int? Port);

public static class ConfigResolver
{
    /// <summary>
    /// Resolve effective bind address + port by layering: defaults → appsettings.json → env vars → CLI.
    /// Pure function — no AppContext, no Environment.GetEnvironmentVariables, no Kestrel.
    /// </summary>
    /// <param name="defaults">Hardcoded defaults (CliOptions.Default).</param>
    /// <param name="appsettingsPath">Absolute path to appsettings.json. Optional — null or non-existent file = skip.</param>
    /// <param name="envOverride">Test-injectable env dict. Pass null in production to read real env vars.</param>
    /// <param name="cli">CLI overrides — only non-null fields are applied.</param>
    public static (string BindAddress, int Port) Resolve(
        CliOptions defaults,
        string? appsettingsPath,
        IDictionary<string, string?>? envOverride,
        CliOverrides cli)
    {
        var builder = new ConfigurationBuilder();

        if (!string.IsNullOrEmpty(appsettingsPath))
            builder.AddJsonFile(appsettingsPath, optional: true, reloadOnChange: false);

        if (envOverride is not null)
            builder.AddInMemoryCollection(envOverride);  // test path
        else
            builder.AddEnvironmentVariables(prefix: "FLAUI_MCP_");  // production path

        var config = builder.Build();

        var bind = cli.BindAddress
                   ?? config["Server:BindAddress"]
                   ?? defaults.BindAddress;

        int port;
        if (cli.Port is int cliPort)
            port = cliPort;
        else if (int.TryParse(config["Server:Port"], out var cfgPort))
            port = cfgPort;
        else
            port = defaults.Port;

        return (bind, port);
    }
}
```

**Why this shape works:**

- **Testable without filesystem**: pass `appsettingsPath = null` for tests that don't want a file; pass a path to a temp-dir file for tests that do.
- **Testable without real env vars**: pass an `IDictionary<string,string?>` with **already-translated keys** (e.g. `["Server:Port"] = "5000"`). Tests don't need to know about prefix-stripping or `__` translation — those are production-only concerns covered by one integration-style smoke test if desired.
- **Per-key reads** sidestep the `Get<T>()`-with-empty-section trap (Gotcha #4) and give silent fall-through on malformed `Port` (Gotcha #5).
- **CLI presence test is `cli.X is not null`**, which is exactly the "flag was passed" semantic CONTEXT mandates.

### Production wiring in `Program.cs` (around line 17)

```csharp
var cli = FlaUI.Mcp.CliOptions.Parse(args);
var cliOverrides = FlaUI.Mcp.CliOptions.ParseOverrides(args);  // new — returns CliOverrides

var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var (bindAddress, port) = ConfigResolver.Resolve(
    defaults: FlaUI.Mcp.CliOptions.Default,
    appsettingsPath: appsettingsPath,
    envOverride: null,         // null → read real env vars with FLAUI_MCP_ prefix
    cli: cliOverrides);

// Override the resolved fields back onto a CliOptions-shaped object so all
// downstream code (logger.Info, HTTP transport, SSE transport) keeps using opts.BindAddress / port.
var opts = cli with { BindAddress = bindAddress, Port = port };
```

The `with` expression keeps every other CLI flag (`Silent`, `Debug`, `Console`, `Transport`, etc.) exactly as parsed — only `BindAddress` and `Port` get the merged values.

## Test strategy

Project: `tests/FlaUI.Mcp.Tests`. Test file: `ConfigResolverTests.cs` alongside existing `CliParserTests.cs`.

```csharp
public class ConfigResolverTests
{
    [Fact] public void NoFileNoEnvNoCli_YieldsDefaults() { /* appsettingsPath=null, env={}, cli=(null,null) */ }

    [Fact] public void FileOnly_AppliesFileValues() { /* write temp appsettings.json with Server section */ }

    [Fact] public void CliOnly_NoFile_AppliesCliValues() { /* appsettingsPath=null, cli=("0.0.0.0", 4000) */ }

    [Fact] public void CliBeatsFile_EvenWhenCliMatchesDefault() {
        // CONTEXT key requirement: --bind 127.0.0.1 (matches default) still beats appsettings.json: 0.0.0.0
        // appsettingsPath = temp file with Server:BindAddress=0.0.0.0
        // cli = ("127.0.0.1", null)
        // expected: bind = "127.0.0.1"
    }

    [Fact] public void EnvBeatsFileButLosesToCli() {
        // appsettingsPath = temp file with Server:Port=2000
        // env = { "Server:Port" = "3000" }    // already-translated key
        // cli = (null, 4000)
        // expected: port = 4000
    }

    [Fact] public void EnvBeatsFile_NoCli() {
        // env = { "Server:Port" = "3000" }
        // cli = (null, null)
        // expected: port = 3000
    }

    [Fact] public void MalformedFilePort_FallsThroughToDefault() {
        // Server:Port = "not-a-number"
        // expected: port = 3020 (no exception)
    }

    [Fact] public void MissingFile_SilentFallback() {
        // appsettingsPath = "C:\nonexistent\appsettings.json"
        // optional:true ensures no exception
    }
}
```

Helper for temp-file tests (xUnit + `IDisposable` fixture or `using var temp = new TempJsonFile(...)`):

```csharp
private static string WriteTempAppsettings(string json) {
    var path = Path.Combine(Path.GetTempPath(), $"appsettings-test-{Guid.NewGuid():N}.json");
    File.WriteAllText(path, json);
    return path;
}
```

(Cleanup: register a `Dispose` or use `[Fact]` + `try/finally`. Acceptable to leak in `%TEMP%` — tests are short-lived.)

**Existing 9 tests in `CliParserTests.cs` must still pass unchanged.** That's the regression bar.

## Help-text update (Program.cs ~line 52-53)

```csharp
Console.WriteLine("  --bind <addr>       Kestrel bind address (default: 127.0.0.1; use 0.0.0.0 for LAN)");
Console.WriteLine("  --port <number>     Listen port (default: 3020)");
Console.WriteLine();
Console.WriteLine("Configuration sources (lowest → highest precedence):");
Console.WriteLine("  defaults  <  appsettings.json (Server section)  <  env (FLAUI_MCP_Server__BindAddress, FLAUI_MCP_Server__Port)  <  --bind/--port");
Console.WriteLine("  appsettings.json is read from the directory containing FlaUI.Mcp.exe and is optional.");
```

One block, four lines, fits the existing `Console.WriteLine` style.

## Sources

### Primary (HIGH confidence)
- [MS Learn — Configuration providers in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers) — confirmed prefix stripping, `__→:` translation, file-provider key case-insensitivity, optional-file semantics.
- `src/FlaUI.Mcp/CliOptions.cs` — verified current parser state and `Default` record.
- `src/FlaUI.Mcp/Program.cs:17, 52-53, 121, 259-265` — verified consumer call sites.
- `src/FlaUI.Mcp/FlaUI.Mcp.csproj:19` — verified `Microsoft.AspNetCore.App` framework reference (Configuration packages transitively available, no NuGet additions needed).
- `tests/FlaUI.Mcp.Tests/CliParserTests.cs` — verified existing test style baseline.

### Confidence breakdown
- Env var behavior (prefix, `__→:`, case-insensitivity): **HIGH** — official MS Learn docs.
- `Get<T>()` vs per-key read tradeoff: **HIGH** — well-known `ConfigurationBinder` behavior.
- Resolver shape recommendation: **HIGH** — pure function, no framework surface, matches established testability patterns in this codebase.
- CLI "flag was passed" detection (Option A vs B vs C): **MEDIUM** — three valid options, Option A is the recommended trade-off but the planner could pick B or C with no functional difference.

**Research date:** 2026-05-01
**Valid until:** ~2026-06-01 (.NET 8 LTS, Configuration API stable since .NET Core 2.0; very low churn).
