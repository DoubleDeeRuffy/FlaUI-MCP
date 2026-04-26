---
phase: 4-streamable-http-transport
plan: 01
subsystem: test-infra,cli-parsing,requirements
tags: [wave-0, xunit, http-transport, scaffolding]
requirements_completed: [HTTP-08]
requirements_declared: [HTTP-01, HTTP-02, HTTP-03, HTTP-04, HTTP-05, HTTP-06, HTTP-07, HTTP-08]
provides:
  - xunit test project wired into FlaUI-MCP.sln
  - testable CliOptions record with Parse/Default
  - skipped test stubs for HTTP-01..07
  - HTTP-01..08 declarations in REQUIREMENTS.md (pending)
key_files:
  created:
    - FlaUI-MCP.sln
    - src/FlaUI.Mcp/CliOptions.cs
    - tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj
    - tests/FlaUI.Mcp.Tests/TestCategories.cs
    - tests/FlaUI.Mcp.Tests/CliParserTests.cs
    - tests/FlaUI.Mcp.Tests/HttpTransportTests.cs
    - tests/FlaUI.Mcp.Tests/SseTransportTests.cs
    - tests/FlaUI.Mcp.Tests/OriginMiddlewareTests.cs
    - tests/FlaUI.Mcp.Tests/ToolParityTests.cs
  modified:
    - src/FlaUI.Mcp/Program.cs
    - src/FlaUI.Mcp/FlaUI.Mcp.csproj
    - .gsd/milestones/1.0/REQUIREMENTS.md
    - .mcp.json
metrics:
  tasks: 3
  duration: ~2h (interrupted by NuGet auth gate, resumed)
  completed: 2026-04-26
---

# Phase 4 Plan 01: Test Infrastructure & CLI Extraction Summary

Wave 0 scaffolding for Phase 4 Streamable HTTP transport: established xunit test project, extracted CLI parsing into a unit-testable `CliOptions` record (with `--bind` and HTTP-08 default-http behavior), and declared HTTP-01..08 in REQUIREMENTS.md so Wave 1 plans have legitimate IDs to claim.

## Resumption Note

This plan was paused mid-execution on a NuGet authentication gate during `dotnet restore` after the previous executor ran `dotnet clean` / wiped `obj/`. The user resolved the auth issue manually. On resumption:

- Tasks 1 (CliOptions extraction) and 2 (test project + stubs + .sln) had already been completed on disk by the prior agent — `CliOptions.cs`, `tests/FlaUI.Mcp.Tests/*`, and `FlaUI-MCP.sln` were uncommitted.
- Task 3 (REQUIREMENTS.md HTTP-01..08 declaration) was the only outstanding gap.
- Resumption agent verified the tree, ran `dotnet build` (succeeded — no clean, no obj wipe), ran `dotnet test` (9 passed / 6 skipped), edited REQUIREMENTS.md to add the HTTP-* section + traceability rows + bumped coverage 32→40, and committed everything as a single phase step.
- Port 3020 was already consistent across `CliOptions.Default`, `.mcp.json`, and the existing `--help` text (`default: 3020` confirmed in Program.cs).

## What Was Built

### Task 1 — CliOptions extraction
- New `src/FlaUI.Mcp/CliOptions.cs`: `public sealed record CliOptions(...)` with 11 fields including new `BindAddress`. Static `Parse(string[])` mirrors legacy switch and adds `--bind <addr>`. Static `Default` exposes `Transport="http"`, `Port=3020`, `BindAddress="127.0.0.1"`.
- `Program.cs` lines 15-76 replaced with `var opts = FlaUI.Mcp.CliOptions.Parse(args);` plus locals; help-print block kept verbatim per plan note.
- Stale help-text inconsistency (`default: 3020` is correct here — earlier plan note about `8080` no longer applies as the help text was already updated to `3020`).

### Task 2 — xunit test project
- `tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj`: net8.0-windows, UseWindowsForms=true, packages: Microsoft.NET.Test.Sdk 17.x, xunit 2.x, xunit.runner.visualstudio 2.x, Microsoft.AspNetCore.Mvc.Testing 8.x, coverlet.collector 6.x. ProjectReference to FlaUI.Mcp.
- `TestCategories.cs`: `public const string Manual = "Manual"` for live-client gating.
- `CliParserTests.cs`: 9 passing tests covering HTTP-08 (`DefaultTransportIsHttp`, `ParseEmptyArgsYieldsDefaultTransport`), default port 3020, default bind loopback, `--bind` parsing, `--port`/`--transport` overrides.
- Skipped stubs: `HttpTransportTests` (HTTP-01/03/04), `SseTransportTests` (HTTP-02), `OriginMiddlewareTests` (HTTP-07), `ToolParityTests` (HTTP-05) — all with `Wave 1: HTTP-XX` skip reasons.
- New `FlaUI-MCP.sln` with both projects under `src/` and `tests/` solution folders.

### Task 3 — REQUIREMENTS.md
- New `### Streamable HTTP Transport` section between TSK and v2, with HTTP-01..08 as `[ ]` pending.
- 8 traceability rows appended (`Phase 4 | Pending`).
- Coverage bumped 32→40, footer updated to `2026-04-26 after Phase 4 Wave 0 declaration`.

## Verification

- `dotnet build FlaUI-MCP.sln -c Debug --nologo` → 0 warnings, 0 errors.
- `dotnet test tests/FlaUI.Mcp.Tests --nologo` → 9 passed, 6 skipped, 0 failed (15 total).
- `Select-String REQUIREMENTS.md HTTP-0[1-8]` → 16 hits (8 checkbox + 8 traceability).

## Deviations from Plan

None — plan executed as written. No Rule-1/2/3 auto-fixes triggered. The auth-gate interruption was resolved by the user out-of-band.

## Self-Check: PASSED

- FlaUI-MCP.sln: FOUND
- src/FlaUI.Mcp/CliOptions.cs: FOUND
- tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj: FOUND
- All 5 test files: FOUND
- REQUIREMENTS.md HTTP-01..08: FOUND (16 hits)
- Build: PASS
- Tests: PASS (9/6/0)
