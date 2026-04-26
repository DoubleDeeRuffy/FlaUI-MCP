---
phase: 4-streamable-http-transport
plan: 04
subsystem: transport-wiring
tags: [transport, http, sse, kestrel, cli, requirements]
requires: [4-01, 4-02, 4-03]
provides: integrated default-http MCP transport binary
affects: [src/FlaUI.Mcp/Program.cs, .gsd/milestones/1.0/REQUIREMENTS.md]
tech-stack:
  added: []
  patterns: [three-way-transport-switch, cli-bind-address-flow]
key-files:
  created: []
  modified:
    - src/FlaUI.Mcp/Program.cs
    - .gsd/milestones/1.0/REQUIREMENTS.md
decisions:
  - Three-way transport switch (http/sse/stdio) with explicit error+Exit(2) on unknown transport
  - NLog console-target gate widened from sse-only to "transport != stdio" (preserves stdio frame purity)
  - Firewall rule (SVC-07) created for both http and sse transports
metrics:
  duration: ~5 min
  completed: 2026-04-26
  tasks: 2
  files_modified: 2
---

# Phase 4 Plan 04: Streamable HTTP Wiring Summary

Wired Plan 4-02's HttpTransport and Plan 4-03's bind-address-aware SseTransport into Program.cs, flipped the binary default to http on /mcp:3020, and closed out HTTP-* requirements.

## Tasks Completed

| # | Name                                                                          | Status |
|---|-------------------------------------------------------------------------------|--------|
| 1 | Wire http branch, --bind, default flip, firewall + NLog gates, help text      | done   |
| 2 | Flip REQUIREMENTS.md HTTP-02 + HTTP-06 from Pending to Complete               | done   |

## Integrated Transport Switch

```csharp
if (transport == "http")
    await FlaUI.Mcp.Mcp.Http.HttpTransport.RunAsync(
        sessionManager, elementRegistry, toolRegistry,
        opts.BindAddress, port, cts.Token);
else if (transport == "sse")
    await new SseTransport(server, opts.BindAddress, port).RunAsync(cts.Token);
else if (transport == "stdio")
    await server.RunAsync(cts.Token);
else
    { logger?.Error("Unknown transport ..."); Environment.Exit(2); }
```

## Help Text (now)

```
  --transport <type>  Transport: http (default), sse, or stdio
  --bind <addr>       Kestrel bind address (default: 127.0.0.1; use 0.0.0.0 for LAN)
  --port <number>     Listen port (default: 3020)
```

The pre-existing `(default: 8080)` typo at line 71 has been corrected to `(default: 3020)`.

## Gate Widenings

- **Firewall (SVC-07):** `if (transport == "sse" || transport == "http")` — both transports now get the rule via Skoosoft FirewallManager.
- **NLog console target:** `enableConsoleTarget: transport != "stdio"` — http and sse both light up the console target; stdio stays frame-clean (Pitfall 2 closed).
- **Startup banner:** Now logs transport, bind, port, debug for diagnostics.

## Pitfalls Closed (per RESEARCH.md)

- **Pitfall 2 (NLog stdout pollution on stdio):** `transport != "stdio"` gate guarantees stdio never gets ConsoleTarget.
- **Pitfall 5 (firewall absent for http):** http branch now matched by SVC-07 gate.

## Requirements Flipped

| ID      | Was     | Now      |
|---------|---------|----------|
| HTTP-02 | Pending | Complete |
| HTTP-06 | Pending | Complete |

(HTTP-01, 03, 04, 05, 07, 08 were already Complete from Plans 4-01..4-03.) All 8 HTTP-* requirements now traced Complete.

## Verification

- `dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -c Debug --nologo` → 0 errors, 3 pre-existing warnings (MCP9004 EnableLegacySse obsolete, CS0414 unused fields).
- `dotnet test tests/FlaUI.Mcp.Tests --nologo` → 19/19 passed, 0 failed, 0 skipped.
- Help text grep: `http (default)`, `default: 3020`, `--bind` all present; `default: 8080` removed.
- Acceptance regex hits all confirmed (HttpTransport.RunAsync x1, opts.BindAddress x2, gate widenings x1 each).

## Deployment Note

Existing `--task` scheduled tasks created with no flags inherit the new default `http` transport — clients pointing at `/sse` will need to either switch to `/mcp` or add `--transport sse` to the scheduled task argument list. Documented for /gsd:verify-work UAT.

## Manual Smoke Plan (deferred to /gsd:verify-work)

1. `FlaUI.Mcp.exe` (no args) → expect listen on 127.0.0.1:3020/mcp via netstat.
2. `FlaUI.Mcp.exe --transport sse` → /sse + /messages reachable.
3. `FlaUI.Mcp.exe --bind 0.0.0.0` → LAN bind verified.
4. Claude Code "type":"http" client round-trip: initialize → tools/list → invoke one tool.

## Deviations from Plan

None — plan executed exactly as written. The two requirements that had to flip (HTTP-02, HTTP-06) were the only Pending HTTP-* rows; HTTP-01, 03, 04, 05, 07, 08 were already Complete from earlier waves so the plan's "HTTP-01..08 flip" task scope reduced organically to just the two genuinely-pending rows.

## Self-Check: PASSED
- src/FlaUI.Mcp/Program.cs modified — FOUND
- .gsd/milestones/1.0/REQUIREMENTS.md modified — FOUND
- Commit a207e0b — FOUND
