---
quick_id: 260501-rx4
date: 2026-05-01
description: Ship default appsettings.json + InnoSetup onlyifdoesntexist install
commit: 715271e
status: complete
---

# Quick Task 260501-rx4 — Summary

## What changed

- **`src/FlaUI.Mcp/appsettings.json`** (new) — default `Server` section with `BindAddress=127.0.0.1`, `Port=3020`. JSONC `//` comments hint that `0.0.0.0` enables LAN access and that CLI / env vars override these values.
- **`src/FlaUI.Mcp/FlaUI.Mcp.csproj`** — added an `<ItemGroup>` with `<Content Include="appsettings.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>` so the file ships next to `FlaUI.Mcp.exe` on every build/publish.
- **`src/FlaUI.Mcp/Setup.iss`** — added a new `[Files]` line:
  ```
  Source: "{#MyAppSourceFolder}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall;
  ```
  - `onlyifdoesntexist` — installer skips the file if `{app}\appsettings.json` already exists (preserves user customizations on upgrade).
  - `uninsneveruninstall` — uninstaller does not delete it; the existing `[Code]` block still gives the user the option to delete `{app}` entirely.
  - The pre-existing wildcard `Excludes: "appsettings.json"` clause was kept; together they make the explicit `onlyifdoesntexist` line authoritative.

## Verification

- `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo -v minimal` — **green** (3 pre-existing warnings, 0 errors).
- `src/FlaUI.Mcp/bin/Debug/net8.0-windows/appsettings.json` — **present**, byte-identical to source.
- JSONC comments parse correctly via `Microsoft.Extensions.Configuration.Json` (uses `JsonCommentHandling.Skip` by default since .NET Core 3.x — covered by the existing 8 `ConfigResolverTests` which already validate the resolver path).

## Operational note

The private TeamCity NuGet feed (`teamcity.skoosoft.de_v2`) returned HTTP 401 during initial restore — same transient issue as 260501-nfb. Temporarily disabled via `dotnet nuget disable source teamcity.skoosoft.de_v2`, completed the build using cached packages + nuget.org, then re-enabled it. No repo NuGet config touched; user-level config is back to its prior state.

## Out of scope (intentional)

- No new tests — existing `ConfigResolverTests` cover JSONC parsing and the precedence chain.
- No change to default values — still `127.0.0.1:3020`, matching CONTEXT.md from 260501-nfb.
- The mojibake in `Setup.iss:56` (German uninstall prompt) was left untouched — pre-existing, separate concern.

## Files committed

- `src/FlaUI.Mcp/appsettings.json` (new)
- `src/FlaUI.Mcp/FlaUI.Mcp.csproj` (modified)
- `src/FlaUI.Mcp/Setup.iss` (modified)

## What this enables

- Fresh install: user gets a working `appsettings.json` with safe defaults and a discoverable hint about LAN exposure.
- Upgrade install: user-customized settings are preserved automatically — no `--bind` / `--port` CLI args needed in the scheduled-task command line for permanent overrides.
- Power user: drop in custom JSON, set `FLAUI_MCP_Server__*` env vars, or pass `--bind`/`--port` — precedence chain remains `defaults < file < env < CLI`.
