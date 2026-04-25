# Architecture

**Analysis Date:** 2026-03-17

## Pattern Overview

**Overall:** Server-side MCP (Model Context Protocol) implementation exposing Windows UI Automation as JSON-RPC tools.

**Key Characteristics:**
- Tool-based architecture: Each Windows automation capability (launch, snapshot, click, type, etc.) is exposed as an MCP tool
- Ref-based element interaction: Elements are identified by semantic refs (e.g., `w1e5`) maintained in a registry, not coordinates
- Accessibility-first: Leverages Windows UI Automation APIs used by screen readers for semantic understanding
- Transport-agnostic: Supports both stdio (JSON-RPC over stdin/stdout) and HTTP SSE transports
- Request-response pattern: Stateless tool execution with session management for launched apps and registered windows

## Layers

**Transport Layer:**
- Purpose: Handle communication between MCP client and server
- Location: `src/FlaUI.Mcp/Mcp/SseTransport.cs`, `src/FlaUI.Mcp/Program.cs`
- Contains: HTTP endpoint handlers for SSE streaming (`/sse`), message POST (`/messages`), stdio input/output handling
- Depends on: McpServer for request handling, Protocol definitions for JSON-RPC format
- Used by: MCP clients (Claude, GitHub Copilot) to invoke tools over stdio or HTTP

**Protocol/MCP Layer:**
- Purpose: Define JSON-RPC message format and MCP-specific types (tools, capabilities, responses)
- Location: `src/FlaUI.Mcp/Mcp/Protocol.cs`
- Contains: Records for JsonRpcRequest, JsonRpcResponse, McpTool, McpToolResult, McpContent; JSON serialization options
- Depends on: System.Text.Json for serialization
- Used by: All layers for request/response serialization

**Server/Orchestration Layer:**
- Purpose: Central MCP server - routes requests to tools, manages tool registry, handles initialize/tools/call methods
- Location: `src/FlaUI.Mcp/Mcp/McpServer.cs`
- Contains: Request routing logic (`initialize` → HandleInitialize, `tools/list` → HandleToolsList, `tools/call` → HandleToolCallAsync), error handling with JSON-RPC error responses
- Depends on: ToolRegistry for tool execution, Protocol types
- Used by: Transport layer (SseTransport, stdio handler in Program.cs)

**Tool Layer:**
- Purpose: Implements all user-facing automation capabilities as MCP tools
- Location: `src/FlaUI.Mcp/Tools/` directory
- Contains: Tool implementations (LaunchTool, SnapshotTool, ClickTool, TypeTool, FillTool, GetTextTool, ScreenshotTool, WindowTools, BatchTool)
- Base class: `ToolBase` in `ToolRegistry.cs` provides common patterns for tool definition, argument extraction, result formatting
- Depends on: SessionManager, ElementRegistry, SnapshotBuilder, Protocol types
- Used by: McpServer via ToolRegistry

**Core/State Management Layer:**
- Purpose: Maintain state for Windows sessions, element references, and accessibility trees
- Location: `src/FlaUI.Mcp/Core/` directory
- Contains: SessionManager (windows/app lifecycle), ElementRegistry (ref ↔ AutomationElement mapping), SnapshotBuilder (UI tree serialization)
- Depends on: FlaUI.Core, FlaUI.UIA3 for UI Automation
- Used by: Tool implementations for element access and window management

**UI Automation Layer (External):**
- Purpose: Interact with Windows accessibility APIs
- Library: FlaUI.Core (v5.0.0), FlaUI.UIA3 (v5.0.0)
- Provides: AutomationElement tree, control patterns (Invoke, Toggle, Value, SelectionItem), property access

## Data Flow

**Snapshot & Interaction Flow:**

1. Client calls `windows_snapshot { "handle": "w1" }` via MCP JSON-RPC
2. McpServer routes to SnapshotTool.ExecuteAsync()
3. SnapshotTool retrieves Window from SessionManager via handle
4. SnapshotBuilder.BuildSnapshot():
   - Traverses FlaUI AutomationElement tree from window root
   - For each meaningful element, extracts: role (from ControlType), name, state (disabled/checked/etc.)
   - Registers element in ElementRegistry, generating ref (e.g., `w1e5`)
   - Builds text representation: `- button "Click Me" [ref=w1e5] [disabled]`
5. Returns formatted tree as text content
6. Client extracts refs from snapshot and uses them in subsequent calls

**Element Interaction Flow:**

1. Client calls `windows_click { "ref": "w1e5" }` via MCP JSON-RPC
2. McpServer routes to ClickTool.ExecuteAsync()
3. ClickTool retrieves AutomationElement from ElementRegistry by ref
4. Attempts invocation in order of preference:
   - Invoke pattern (most reliable for buttons)
   - Toggle pattern (for checkboxes)
   - SelectionItem pattern (for list items)
   - Fall back to mouse click via FlaUI.Core.Input.Mouse
5. Returns result as text content

**Window Management Flow:**

1. Client calls `windows_launch { "app": "calc.exe" }` via MCP JSON-RPC
2. LaunchTool calls SessionManager.LaunchApp()
3. SessionManager uses Process.Start() to launch executable
4. Waits for process ready (WaitForInputIdle, extra Thread.Sleep for UI appearance)
5. Finds window by process ID or window title search
6. Registers window in SessionManager._windows dict with generated handle (e.g., `w1`)
7. Returns handle for use in subsequent calls

**State Management:**

- **SessionManager**: Maps window handles (`w1`, `w2`) → FlaUI Window objects. Maintains singleton UIA3Automation instance. Lifecycle tied to MCP server process.
- **ElementRegistry**: Maps element refs (`w1e5`, `w2e12`) → AutomationElement objects. Refs are scoped to window and regenerated on each snapshot call (ClearWindow before rebuild).
- **Tool Execution**: Each tool call is synchronous, read-only for snapshots/queries, mutating for interactions (click/type). No transaction management - each action is independent.

## Key Abstractions

**ElementRegistry:**
- Purpose: Persistent semantic ID for elements within a window session
- Ref format: `{windowHandle}e{counter}` (e.g., `w1e5`)
- Pattern: Dictionary-based lookup, thread-safe through ToolRegistry's synchronous execution
- Use case: Allows agents to reference specific UI elements across multiple tool calls without retraining on screen layout
- Files: `src/FlaUI.Mcp/Core/ElementRegistry.cs`

**SnapshotBuilder:**
- Purpose: Convert FlaUI AutomationElement tree into agent-friendly text representation
- Pattern: Depth-limited recursive tree walk with element filtering and role mapping
- Role mapping: Converts FlaUI ControlType enum to ARIA-like roles (button, textbox, checkbox, etc.)
- Filtering: Skips decorative elements (unnamed separators, scrollbars) while preserving actionable elements
- Files: `src/FlaUI.Mcp/Core/SnapshotBuilder.cs`

**ToolBase:**
- Purpose: Common interface and utilities for all tool implementations
- Pattern: Abstract base class with template methods for definition/execution; helper methods for argument extraction
- Argument parsing: `GetStringArgument()`, `GetArgument<T>()`, `GetBoolArgument()` handle JSON deserialization from JsonElement
- Result formatting: `TextResult()`, `ErrorResult()`, `ImageResult()` wrap content in McpToolResult
- Files: `src/FlaUI.Mcp/Mcp/ToolRegistry.cs`

**ToolRegistry:**
- Purpose: Central directory mapping tool names to implementations
- Pattern: Dictionary<string, ITool> with sync registration and async execution
- Execution: Routes tool name to implementation, catches exceptions, returns JSON-RPC error response
- Files: `src/FlaUI.Mcp/Mcp/ToolRegistry.cs`

## Entry Points

**Program.cs (`src/FlaUI.Mcp/Program.cs`):**
- Location: Project root
- Triggers: Process startup (dotnet run or compiled .exe)
- Responsibilities:
  - Parse CLI args (--transport stdio|sse, --port)
  - Create singleton SessionManager, ElementRegistry, and all tool instances
  - Register tools in ToolRegistry
  - Create McpServer
  - Start transport (stdio or SSE HTTP) and run until cancellation
  - Cleanup (SessionManager.Dispose) on shutdown

**McpServer.RunAsync() - Stdio Transport:**
- Reads JSON-RPC messages line-by-line from stdin
- For each line: deserialize, route to HandleRequestAsync, serialize response to stdout
- Runs until EOF or cancellation token

**SseTransport.RunAsync() - HTTP Transport:**
- Starts ASP.NET Core web server on specified port
- Maps GET /sse: Opens SSE stream, sends endpoint URL, holds connection
- Maps POST /messages: Receives JSON-RPC request, processes via McpServer.HandleRequestAsync, sends response back via SSE
- Session-based: Each client gets unique sessionId to pair SSE stream with message endpoints

## Error Handling

**Strategy:** Synchronous exception catching with JSON-RPC error response format. No retry logic or circuit breakers.

**Patterns:**

- **Tool Execution Errors**: Caught in McpServer.HandleRequestAsync() and ToolRegistry.ExecuteToolAsync(), wrapped in JsonRpcError (code: -32603, message: exception message)
- **Tool Validation Errors**: Explicit error checks in individual tools (missing args, window not found, element not found) return ErrorResult() with descriptive text
- **Transport Errors**: Handled in SseTransport message parsing (malformed JSON, missing sessionId) return 400 status with text explanation
- **FlaUI Access Errors**: Caught in SnapshotBuilder and tool implementations with empty fallbacks (return default values, skip elements) to handle transient UI automation issues

**Example Error Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32603,
    "message": "Window not found: w99"
  }
}
```

## Cross-Cutting Concerns

**Logging:**
- Approach: Console.Error for diagnostics (SseTransport logs startup info, McpServer logs request errors)
- No persistent logging or structured logs
- Errors also printed to stderr to aid debugging

**Validation:**
- Approach: Per-tool validation of required arguments before execution
- UI element validation: GetElement returns null if ref not found; tools check and return ErrorResult
- No global schema validation; MCP client responsible for conforming to inputSchema

**Authentication:**
- Approach: None. MCP runs as local subprocess or localhost HTTP. Trust boundary is process invocation by client.
- SSE transport has session-based pairing but no authentication between client and server

**State Isolation:**
- Approach: No multi-tenancy. Single SessionManager serves all concurrent tool calls.
- Window handles global within a server instance
- Element refs scoped to window but regenerated per snapshot (no persistence across calls)

