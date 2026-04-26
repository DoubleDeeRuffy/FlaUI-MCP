# Phase 4: Streamable HTTP transport - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-26
**Phase:** 4-streamable-http-transport
**Areas discussed:** SDK vs hand-roll, CLI flag & default, Co-existence model, Session-Id & resumability, Bind & Origin

---

## SDK vs hand-roll

| Option | Description | Selected |
|--------|-------------|----------|
| Use ModelContextProtocol SDK's built-in Streamable HTTP (MapMcp) | SDK already implements the spec; minimal new code | ✓ |
| Hand-roll /mcp with our own JSON-RPC dispatcher | Full control, no dependency on SDK transport surface | |
| Hybrid — SDK for /mcp, keep hand-rolled SseTransport for legacy | Don't churn working code | |

**User's choice:** a (SDK built-in)
**Notes:** Researcher must verify exact SDK API and minimum package version.

---

## CLI flag value & default transport

| Option | Description | Selected |
|--------|-------------|----------|
| `--transport http`, default flips to `http` | Short, matches client config "type": "http" | ✓ |
| `--transport http`, keep `sse` as default | Backward-compatible for existing deployments | |
| `--transport streamable-http`, default flips | Verbose, unambiguous, exact spec name | |
| `--transport http` + legacy SSE always live | Zero-churn co-mount | |

**User's choice:** a (`--transport http`, default flips to `http`)
**Notes:** Soft-breaking change for existing sse-pinned configs accepted explicitly.

---

## Co-existence model

| Option | Description | Selected |
|--------|-------------|----------|
| Mutually exclusive per process | One transport selected at startup; simple wiring | ✓ |
| Always co-mounted on HTTP modes | Both /mcp and /sse served simultaneously | |
| Co-mount opt-in via `--legacy-sse` flag | Default exclusive, flag enables co-mount | |

**User's choice:** a (mutually exclusive)
**Notes:** Matches stdio's per-process model; existing SSE deployments stay on `--transport sse` until they migrate.

---

## Session-Id implementation (4a)

| Option | Description | Selected |
|--------|-------------|----------|
| SDK default Session-Id, not bound to SessionManager | SDK handles spec correctness; UI session state stays independent | ✓ |
| Bind Mcp-Session-Id 1:1 to SessionManager | Tighter cleanup of leaked window handles on session close | |
| SDK default + log Session-Id at Info | SDK default plus observability | |

**User's choice:** a (SDK default, independent of SessionManager)

## Stream resumability (4b)

| Option | Description | Selected |
|--------|-------------|----------|
| Out of scope — SDK default only | Optional per spec, no current demand | ✓ |
| In scope — implement event-id + bounded replay | Full spec support | |
| In scope only if SDK provides for free | Opportunistic | |

**User's choice:** a (out of scope, deferred)

---

## Bind address & Origin protection

| Option | Description | Selected |
|--------|-------------|----------|
| 127.0.0.1 default + Origin check + `--bind` escape hatch, applied to both http and sse | Matches MCP spec security guidance, uniform behavior | ✓ |
| Same as a but http only — leave legacy sse as-is | No regression for existing SSE | |
| Keep current Kestrel default, no Origin check | Phase 4 is just add-transport, defer hardening | |
| Localhost-only, no escape hatch | Simplest, hardest "local trust boundary" | |

**User's choice:** a (loopback default + Origin check + escape hatch, both transports)

---

## Claude's Discretion

- SDK package version & exact API call shape (researcher decides)
- Origin validation as middleware vs SDK hook
- Origin-rejection response body wording
- Whether `--bind` includes explicit port override vs reusing `--port`
- HTTP request lifecycle log volume and level
- Internal Program.cs refactor to host the new transport branch

## Deferred Ideas

- Stream resumability (Last-Event-ID replay buffer)
- SessionManager ↔ MCP session-id binding
- Co-mounted transports (one process serves both /mcp and /sse)
- Authenticated remote access
