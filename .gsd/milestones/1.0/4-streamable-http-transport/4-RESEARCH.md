# Phase 4: Streamable HTTP transport - Research

**Researched:** 2026-04-26
**Domain:** MCP transports, ASP.NET Core hosting, ModelContextProtocol C# SDK
**Confidence:** HIGH (spec + SDK API surface verified against current docs)

## Summary

The MCP spec 2025-03-26 defines **Streamable HTTP** as a single endpoint
(`/mcp`) accepting `POST` (JSON-RPC, may upgrade to SSE) and `GET` (optional
SSE for server-initiated messages), governed by an `Mcp-Session-Id` header.
The official C# SDK package `ModelContextProtocol.AspNetCore` (v1.2.0,
released 2025-03-27) implements this transport via `AddMcpServer()
.WithHttpTransport()` + `app.MapMcp()`, registers `POST /`, `GET /`,
`DELETE /` automatically, and exposes legacy `/sse` + `/message` behind an
`EnableLegacySse = true` opt-in. Origin validation, idle-timeout, and
session storage are configurable via `HttpServerTransportOptions`.

**CRITICAL finding contradicting CONTEXT.md assumptions:** the project does
**not** currently use the SDK. `src/FlaUI.Mcp/Mcp/{Protocol,McpServer,
SseTransport,ToolRegistry}.cs` are hand-rolled implementations under
`PlaywrightWindows.Mcp` namespaces. `FlaUI.Mcp.csproj` has **no**
`ModelContextProtocol` package reference. Adopting `MapMcp()` is therefore
not a "version bump" — it's an **introduction** of the SDK plus a rewrite of
how tools register (from custom `ITool`/`ToolRegistry` to the SDK's
`[McpServerTool]` attribute model **or** an adapter that bridges the two).
The planner must decide: (A) keep hand-rolled and add a hand-rolled
Streamable-HTTP layer next to `SseTransport.cs`, or (B) adopt the SDK and
migrate the tool registry. CONTEXT.md D-01 locks in option B. This is the
single biggest scope item the planner needs to size.

**Primary recommendation:** Add `ModelContextProtocol.AspNetCore` 1.2.0,
introduce a thin tool-bridge that re-exposes existing `ITool` instances as
SDK `McpServerTool`s (preserving `ElementRegistry`/`SessionManager` DI),
wire `--transport http` to a Kestrel host that calls
`AddMcpServer().WithHttpTransport(...).MapMcp()`, and keep `--transport sse`
running on the existing hand-rolled `SseTransport` for one milestone before
deprecating it.

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Use the official `ModelContextProtocol` SDK's built-in Streamable
  HTTP support. Wire `/mcp` via the SDK's `MapMcp()` (or current equivalent)
  rather than hand-rolling the endpoint.
- **D-02:** New transport value `--transport http` selects Streamable HTTP.
  Default transport flips from `sse` to `http`. `--transport sse` and
  `--transport stdio` remain available unchanged. Help text/banners must
  reflect the new default.
- **D-03:** Transports are mutually exclusive per process — `--transport http`
  mounts only `/mcp`; `--transport sse` mounts only `/sse` + `/messages`.
  No co-mounting. No `--legacy-sse` flag.
- **D-04:** Use SDK default `Mcp-Session-Id` handling. Do NOT bind MCP
  session-id to FlaUI's `SessionManager`.
- **D-05:** Stream resumability via `Last-Event-ID` is OUT of scope.
- **D-06:** Default Kestrel bind = `127.0.0.1` only. Reject requests whose
  `Origin` header is not `null`/`localhost`/`127.0.0.1`. Add `--bind <addr>`
  CLI escape hatch. Apply same bind+Origin policy to legacy SSE path.

### Claude's Discretion
- Exact SDK package version and API call shape (researcher confirms — see
  Standard Stack).
- Internal refactor of `Program.cs` to host the new transport branch.
- Whether `Origin` validation lives as ASP.NET Core middleware vs SDK hook.
- Exact wording of Origin-rejection responses.
- Whether `--bind` accepts an explicit port override or reuses `--port`.
- Logging volume and level for new HTTP request lifecycle events.

### Deferred Ideas (OUT OF SCOPE)
- Stream resumability (`Last-Event-ID` replay buffer).
- `SessionManager` ↔ MCP session-id binding.
- Co-mounted transports.
- Authenticated remote access.

## Phase Requirements

The roadmap lists 5 success criteria but no requirement IDs. Proposed IDs to
be added to REQUIREMENTS.md during planning:

| ID | Description | Research Support |
|----|-------------|------------------|
| HTTP-01 | `--transport http` selects Streamable HTTP (POST/GET on `/mcp`) | Standard Stack: `MapMcp()` registers POST/GET/DELETE on the supplied path |
| HTTP-02 | `--transport sse` legacy endpoints (`/sse`, `/messages`) keep working | Existing `SseTransport.cs` retained per D-03 |
| HTTP-03 | Modern MCP client (Claude Code `"type":"http"`) can initialize, list tools, invoke tool | SDK handles initialize/tools/list/tools/call out-of-the-box |
| HTTP-04 | `Mcp-Session-Id` header per spec — auto-issued, 400 if missing on non-init, 404 if expired | SDK default behavior; `Stateless=false`, `IdleTimeout` default 2h |
| HTTP-05 | All 11 tools (Launch/Snapshot/Click/Type/Fill/GetText/Screenshot/ListWindows/FocusWindow/CloseWindow/Batch) work identically across transports | Tool-bridge or migration to `[McpServerTool]` attribute |
| HTTP-06 | Default bind = `127.0.0.1`; `--bind <addr>` escape hatch (D-06) | Kestrel `UseUrls("http://127.0.0.1:{port}")` default |
| HTTP-07 | `Origin` header rejected unless null/localhost/127.0.0.1 (D-06) | ASP.NET Core middleware (DNS-rebinding mitigation per spec) |
| HTTP-08 | Default transport flipped from `sse` → `http` (D-02); help text updated | `Program.cs` arg parser + `--help` block |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `ModelContextProtocol.AspNetCore` | **1.2.0** | Streamable HTTP transport for ASP.NET Core | Official Anthropic/MS-maintained SDK; ships `MapMcp()` + `HttpServerTransportOptions` |
| `ModelContextProtocol` | **1.2.0** | Core MCP types (`McpServerTool`, `IMcpServer`, lifecycle) | Required transitively; same release cadence |
| `Microsoft.AspNetCore.App` | (FrameworkRef, already present) | Kestrel host + routing | Already wired via `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in csproj |

### Supporting (already present)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `NLog.Web.AspNetCore` | 5.* | Structured logging on the Kestrel host | Already wired (Phase 1); auto-applies |
| `Skoosoft.Windows` | * | `FirewallManager` for SSE rule | SSE branch only (Program.cs:115) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `ModelContextProtocol.AspNetCore` 1.2.0 | Hand-roll Streamable HTTP next to `SseTransport.cs` | Faster start (no DI / tool-bridge), but reimplements session-id state machine, content-type negotiation, SSE keepalive, DELETE handling — multi-week effort. Locked out by D-01. |
| Adopt SDK fully (replace `Mcp/*.cs`) | Adapter pattern: keep `ITool`, register a single SDK tool that fans out by name | Adapter is smaller delta but less idiomatic. Recommend adapter for Phase 4, full migration as a follow-up. |

**Installation:**
```xml
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.0" />
```

**Version verification:** Latest stable per
`https://github.com/modelcontextprotocol/csharp-sdk/releases` is **1.2.0**
(2025-03-27). 1.2.0 specifically ships "Legacy SSE disabled by default;
stateless HTTP improvements" — exactly what we need. Streamable HTTP itself
landed in 0.7.0-preview.1 (Jan 2025); GA in 1.0.0 (Feb 2025). Target
framework requirement: net8.0+ — our `net8.0-windows` qualifies.

## Architecture Patterns

### Recommended Project Structure (delta from existing)
```
src/FlaUI.Mcp/
├── Mcp/                       # existing hand-rolled (kept)
│   ├── McpServer.cs
│   ├── Protocol.cs
│   ├── SseTransport.cs        # legacy --transport sse path
│   └── ToolRegistry.cs
├── Mcp/Http/                  # NEW — Streamable HTTP via SDK
│   ├── HttpTransport.cs       # builder.Services.AddMcpServer().WithHttpTransport(...).MapMcp()
│   ├── ToolBridge.cs          # adapts ITool → SDK McpServerTool
│   └── OriginValidationMiddleware.cs  # D-06 enforcement
└── Program.cs                 # adds `http` branch + --bind parsing
```

### Pattern 1: SDK Streamable-HTTP host
**What:** Minimal Kestrel app that delegates routing to `MapMcp()`.
**When to use:** `--transport http` branch only.
**Example:**
```csharp
// Source: https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html
var builder = WebApplication.CreateBuilder();

builder.WebHost.UseUrls($"http://{bindAddress}:{port}");
builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(opts =>
    {
        opts.Stateless = false;                    // session-id flow per spec
        opts.IdleTimeout = TimeSpan.FromHours(2);  // default
        opts.EnableLegacySse = false;              // D-03: never co-mount
    })
    .WithTools<FlaUiToolBridge>();                 // OR: per-tool registration

var app = builder.Build();
app.UseMiddleware<OriginValidationMiddleware>();   // D-06
app.MapMcp("/mcp");                                // POST /mcp, GET /mcp, DELETE /mcp
await app.RunAsync(cancellationToken);
```

### Pattern 2: Tool bridge (preserves existing `ITool` registry)
**What:** Adapter exposing existing tools to the SDK without rewriting them.
**Example:**
```csharp
// Bridge each ITool as an SDK McpServerTool via reflection or a helper.
// SDK supports both attribute-driven [McpServerTool] discovery and
// programmatic AddTool(name, handler) registration.
foreach (var tool in toolRegistry.GetAll())
{
    mcpBuilder.Tools.Add(McpServerTool.Create(
        name: tool.Name,
        description: tool.Description,
        inputSchema: tool.InputSchema,
        handler: async (args, ct) => await tool.ExecuteAsync(args, ct)));
}
```

### Pattern 3: Origin validation middleware (D-06)
```csharp
// Source: MCP spec 2025-03-26 §"Security Warning"
public sealed class OriginValidationMiddleware(RequestDelegate next, ILogger<OriginValidationMiddleware> log)
{
    private static readonly string[] AllowedHosts = ["localhost", "127.0.0.1"];
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Headers.TryGetValue("Origin", out var origin))
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var u) ||
                !AllowedHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase))
            {
                log.LogWarning("Rejecting request — Origin {Origin} not in allowlist", origin.ToString());
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsync("Origin not allowed");
                return;
            }
        }
        // null Origin (no header) is allowed per D-06
        await next(ctx);
    }
}
```

### Anti-Patterns to Avoid
- **Co-mounting `MapMcp("/mcp")` and `SseTransport` on the same app** — D-03
  forbids it; also risks port-conflict and confusing session models.
- **Setting `EnableLegacySse = true`** — would mount `/sse` + `/message` on
  the SDK *in addition to* `/mcp`, violating D-03.
- **Binding the MCP session-id to FlaUI `SessionManager`** — explicitly
  forbidden by D-04. They are different lifetimes (HTTP session vs UIA
  window registry).
- **Writing to Console.Error / Console.WriteLine** in HTTP-mode code —
  forbidden by Phase 1 convention; use NLog logger.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| `Mcp-Session-Id` issuance, validation, expiry, 400/404 responses | Custom session table | SDK `HttpServerTransportOptions` | Spec edge cases (visible-ASCII range, idle timeout, DELETE 405) are tedious to get right |
| Content-type negotiation (POST → JSON vs `text/event-stream`) | Branching in handler | SDK `MapMcp()` | Spec mandates client `Accept` header check; SDK already does it |
| SSE keepalive + framing for GET stream | `Response.Body.WriteAsync` loops | SDK | Already battle-tested |
| JSON-RPC 2.0 request/response/batch parsing | Hand-rolled (current `Protocol.cs`) | SDK serializer | Existing impl works for SSE legacy path but should NOT be reused for `/mcp` |
| DNS-rebinding protection | Skipping it | Custom Origin middleware (small, OK to write) | Spec REQUIRES Origin validation; small enough to hand-roll, but document why |

**Key insight:** The Streamable HTTP wire protocol has ~10 distinct status
code requirements (202 for notifications-only POST, 400 for missing
session-id, 404 for expired session, 405 for DELETE-not-allowed, 405 for
GET-without-SSE-support, 403 for bad Origin). The SDK encodes all of these.
Hand-rolling means owning a compliance test matrix.

## Runtime State Inventory

This phase introduces a new transport but does not rename or migrate
anything. State inventory:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — MCP sessions are in-memory in the SDK; FlaUI `SessionManager` is per-process and unchanged | None |
| Live service config | Windows Service install args (Phase 2) bake in a transport flag — verify whether installed services need re-install when default flips from `sse` → `http` | Document upgrade note in SUMMARY: existing services keep `sse` (their service args don't auto-change); fresh installs get `http` default |
| OS-registered state | Scheduled task created via `schtasks /create` (Program.cs:175) — task command line embeds `--debug` if set, but NOT `--transport`. Switching defaults won't break existing scheduled tasks | Document: existing tasks continue using new default (`http`). If user wants old SSE behavior on existing task, they re-register with `--transport sse` |
| Secrets/env vars | None | None |
| Build artifacts | None — adding a NuGet package is auto-restored | None |

## Common Pitfalls

### Pitfall 1: Forgetting `Accept` header dual-listing
**What goes wrong:** Client POSTs to `/mcp` with only `Accept: application/json`; SDK rejects.
**Why it happens:** Spec §"Sending Messages" requires client to list **both** `application/json` AND `text/event-stream`. Some hand-rolled clients miss this.
**How to avoid:** Document for testing — use a real client (Claude Code, MCP Inspector) or a test that explicitly sets the dual Accept header.
**Warning signs:** 406 Not Acceptable in logs.

### Pitfall 2: NLog ConsoleTarget in stdio mode corrupts protocol
**What goes wrong:** Any stdout write under `--transport stdio` corrupts JSON-RPC frames.
**Why it happens:** Phase 1 already gates console target on `transport == "sse"`. Phase 4 must extend that to `transport == "sse" || transport == "http"`.
**How to avoid:** Update `LoggingConfig.ConfigureLogging(... enableConsoleTarget: transport != "stdio")` semantically.
**Warning signs:** stdio client reports JSON parse errors after any tool call.

### Pitfall 3: Origin = `null` confusion
**What goes wrong:** Server rejects requests from CLI tools (curl, MCP Inspector standalone) which send no Origin header at all.
**Why it happens:** "null Origin" can mean either *header absent* or *string `null`* (HTML5 sandboxed iframes).
**How to avoid:** Treat *header absent* as allowed (typical CLI / native clients). Treat string `"null"` as allowed too per D-06.
**Warning signs:** 403 from `curl http://127.0.0.1:3020/mcp` despite localhost.

### Pitfall 4: Default port collision
**What goes wrong:** `Program.cs` defaults `port = 3020` but the help text says default is 8080. Existing SSE listeners may already be bound on 3020.
**Why it happens:** Pre-existing inconsistency in current code (line 24 vs line 71).
**How to avoid:** Pick one canonical default in this phase — recommend keeping 3020 since that's what the binary actually does, and fix the help string. Document the decision.
**Warning signs:** `EADDRINUSE` style error from Kestrel on startup.

### Pitfall 5: `--bind 0.0.0.0` without firewall rule
**What goes wrong:** User passes `--bind 0.0.0.0` for LAN access; Windows Firewall blocks it because `FirewallManager.SetRule` is gated on `transport == "sse"` (Program.cs:115).
**Why it happens:** Firewall logic isn't aware of the new `http` transport.
**How to avoid:** Extend the firewall-rule branch to fire when `transport ∈ {sse, http}`.
**Warning signs:** Server starts cleanly but external clients time out; netstat shows process listening but no traffic.

## Code Examples

### Replacement `Program.cs` transport branch (sketch)
```csharp
// Source: distilled from current Program.cs + SDK getting-started
if (transport == "http")
{
    // Firewall (extend existing logic to fire for http too)
    EnsureFirewallRule(FirewallRuleName, exeFilePath, logger);

    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseUrls($"http://{bindAddress}:{port}");
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddSingleton(sessionManager);
    builder.Services.AddSingleton(elementRegistry);
    builder.Services.AddSingleton(toolRegistry);
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(opts =>
        {
            opts.Stateless        = false;
            opts.EnableLegacySse  = false;
        })
        .WithToolsFromBridge(toolRegistry);  // extension that fans existing ITool list

    var app = builder.Build();
    app.UseMiddleware<OriginValidationMiddleware>();
    app.MapMcp("/mcp");
    await app.RunAsync(cts.Token);
}
else if (transport == "sse")
{
    // unchanged — keep hand-rolled SseTransport for legacy
    var sseTransport = new SseTransport(server, port);
    await sseTransport.RunAsync(cts.Token);
}
else // stdio
{
    await server.RunAsync(cts.Token);
}
```

### CLI parsing additions
```csharp
case "--bind" when i + 1 < args.Length:
    bindAddress = args[++i];   // e.g. "0.0.0.0", default "127.0.0.1"
    break;
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| HTTP+SSE transport (2024-11-05) — separate `/sse` (GET) + `/messages` (POST) | Streamable HTTP (2025-03-26) — single `/mcp` for POST/GET/DELETE | MCP 2025-03-26 spec | Older clients still need `/sse` + `/message`; D-03 keeps the legacy path on a separate `--transport sse` process |
| Hand-rolled JSON-RPC + SSE in `Mcp/Protocol.cs` | SDK `MapMcp()` | This phase | Project gains a SDK dependency for the first time |

**Deprecated/outdated:**
- Project's hand-rolled `SseTransport` corresponds to the OLD 2024-11-05
  HTTP+SSE transport. It's not deprecated *yet* (D-03 keeps it), but plan a
  follow-up milestone to retire it once all clients migrate.

## Open Questions

1. **Tool registration: full migration vs adapter?**
   - What we know: SDK supports `[McpServerTool]` attribute or `McpServerTool.Create(...)` programmatic creation.
   - What's unclear: does Phase 4's scope include rewriting all 11 tools to the attribute model, or only a thin adapter?
   - Recommendation: **Adapter** for Phase 4 (smaller delta, no tool-test churn). File a follow-up phase for full migration.

2. **`--port` semantics under `--bind`?**
   - What we know: Current `--port` is shared between SSE listener and would be shared with HTTP.
   - What's unclear: Does `--bind <addr>` accept `host:port` syntax, or stay address-only and reuse `--port`?
   - Recommendation: address-only (D-06 wording suggests this). `--port` stays orthogonal.

3. **What does Claude Code's `"type": "http"` actually send?**
   - What we know: It uses Streamable HTTP per spec.
   - What's unclear: Does it set `Origin`? Empirically required for AC #3.
   - Recommendation: Capture a real request via Wireshark/network tab during Wave 2 testing; treat absent Origin as the common case.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK | Build | ✓ | (existing CI/dev box) | — |
| `ModelContextProtocol.AspNetCore` 1.2.0 | New transport | ✓ (NuGet, public) | 1.2.0 | — |
| Kestrel | HTTP host | ✓ | bundled with `Microsoft.AspNetCore.App` | — |
| Claude Code (HTTP-mode test client) | AC #3 manual smoke | ✓ on dev box | latest | MCP Inspector CLI as alternative |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | **None detected in repo** — no `*.Tests.csproj`, no `dotnet test` target, no `xunit`/`nunit`/`mstest` references |
| Config file | none — see Wave 0 |
| Quick run command | `dotnet test tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj --filter Category=Http -- --report-trx` (after Wave 0) |
| Full suite command | `dotnet test FlaUI-MCP.sln` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HTTP-01 | `--transport http` mounts POST/GET on `/mcp` | integration (`WebApplicationFactory<>`) | `dotnet test --filter FullyQualifiedName~HttpTransportTests.MapsMcpEndpoint` | ❌ Wave 0 |
| HTTP-02 | `--transport sse` still mounts `/sse` + `/messages` | integration | `dotnet test --filter FullyQualifiedName~SseTransportTests.LegacyEndpointsRespond` | ❌ Wave 0 |
| HTTP-03 | initialize → tools/list → tools/call works over `/mcp` | integration (in-proc HTTP MCP client) | `dotnet test --filter FullyQualifiedName~HttpTransportTests.EndToEndToolCall` | ❌ Wave 0 |
| HTTP-04 | `Mcp-Session-Id` issued on init, 400 without on subsequent | integration | `dotnet test --filter FullyQualifiedName~HttpTransportTests.SessionIdLifecycle` | ❌ Wave 0 |
| HTTP-05 | All 11 tools callable on both transports | integration (parametrized) | `dotnet test --filter FullyQualifiedName~ToolParityTests` | ❌ Wave 0 |
| HTTP-06 | Default bind 127.0.0.1; `--bind 0.0.0.0` widens | manual + smoke | `netstat -ano | findstr :3020` after launch | manual-only |
| HTTP-07 | Bad `Origin` rejected with 403 | integration | `dotnet test --filter FullyQualifiedName~OriginMiddlewareTests.RejectsExternalOrigin` | ❌ Wave 0 |
| HTTP-08 | Default transport is now `http` when no flag passed | unit (CLI parser) | `dotnet test --filter FullyQualifiedName~CliParserTests.DefaultTransportIsHttp` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter Category=Http -m:1` (~10 s once Wave 0 lands)
- **Per wave merge:** `dotnet test FlaUI-MCP.sln` (full suite)
- **Phase gate:** Full suite green + manual Claude Code `"type":"http"` smoke against running binary, before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/FlaUI.Mcp.Tests/FlaUI.Mcp.Tests.csproj` — new test project, references `Microsoft.AspNetCore.Mvc.Testing` for `WebApplicationFactory<>`
- [ ] `tests/FlaUI.Mcp.Tests/Http/HttpTransportTests.cs` — covers HTTP-01, 03, 04
- [ ] `tests/FlaUI.Mcp.Tests/Http/OriginMiddlewareTests.cs` — covers HTTP-07
- [ ] `tests/FlaUI.Mcp.Tests/Sse/SseTransportTests.cs` — covers HTTP-02 (regression)
- [ ] `tests/FlaUI.Mcp.Tests/Tools/ToolParityTests.cs` — covers HTTP-05
- [ ] `tests/FlaUI.Mcp.Tests/Cli/CliParserTests.cs` — covers HTTP-08 (requires extracting CLI parsing from top-level `Program.cs` into a testable static method)
- [ ] Framework install: `dotnet add tests/FlaUI.Mcp.Tests package xunit Microsoft.AspNetCore.Mvc.Testing Microsoft.NET.Test.Sdk xunit.runner.visualstudio`

## Sources

### Primary (HIGH confidence)
- MCP Spec 2025-03-26 — Transports — `https://modelcontextprotocol.io/specification/2025-03-26/basic/transports` — endpoint shape, status codes, Mcp-Session-Id rules, Origin guidance
- C# SDK API: `HttpServerTransportOptions` — `https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.AspNetCore.HttpServerTransportOptions.html` — exact options, endpoints registered by `MapMcp()`
- C# SDK Releases — `https://github.com/modelcontextprotocol/csharp-sdk/releases` — version 1.2.0 (2025-03-27), Streamable-HTTP-since-0.7.0-preview.1
- C# SDK Getting Started — `https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html` — `AddMcpServer().WithHttpTransport().MapMcp()` shape
- Project source: `src/FlaUI.Mcp/Program.cs`, `src/FlaUI.Mcp/FlaUI.Mcp.csproj`, `src/FlaUI.Mcp/Mcp/{McpServer,Protocol,SseTransport,ToolRegistry}.cs` — current hand-rolled implementation
- Basic-memory: `main/projects/flaui-mcp/fla-ui-mcp-architecture` — opaque `wNeM` ref model
- Basic-memory: `main/architecture/mcp-servers/c-mcp-server-patterns` — JSON-RPC + tool-registry conventions
- Basic-memory: `main/projects/mssql-mcp/dual-transport-and-dialect-strategy` — single tool registry serving multiple transports

### Secondary (MEDIUM confidence)
- DeepWiki — `https://deepwiki.com/modelcontextprotocol/csharp-sdk/5.4-streamable-http-protocol` — corroborates endpoint registration list
- Auth0 blog on Streamable HTTP — `https://auth0.com/blog/mcp-streamable-http/` — security rationale for replacing legacy SSE

### Tertiary (LOW confidence — flagged for validation during planning)
- Whether SDK 1.2.0 `MapMcp(string pattern)` accepts a custom path argument (e.g. `MapMcp("/mcp")`) vs only `MapMcp()` at root. Verify by reading the SDK source on first task; if path-arg is missing, mount via `app.UseRouting()` + `Map("/mcp", ...)`.

### Memory Gap
- No prior basic-memory note on `MapMcp` / `HttpServerTransportOptions` / `Mcp-Session-Id` specifically. **Action item:** end-of-phase, write a durable note `projects/FlaUI-MCP/transports/streamable-http.md` documenting the final wiring used, package version, and test recipe — so a future phase doesn't redo this research.

## Metadata

**Confidence breakdown:**
- Standard stack: **HIGH** — version, API, endpoints all verified against official SDK docs and release notes.
- Architecture: **HIGH** — pattern is the documented SDK happy path; tool-bridge approach mitigates the project's hand-rolled tool registry.
- Pitfalls: **MEDIUM** — pitfalls 1-3 are spec/SDK-driven (HIGH); pitfalls 4-5 are project-specific inferences from reading `Program.cs` (MEDIUM, validated during planning).
- Validation architecture: **MEDIUM** — no test infra exists yet; full Wave 0 work is required before HTTP-* tests can run.

**Research date:** 2026-04-26
**Valid until:** 2026-05-26 (30 days — SDK 1.2.0 is stable; spec 2025-03-26 supersedes the 2024-11-05 transport but stays the current MCP version of record)
