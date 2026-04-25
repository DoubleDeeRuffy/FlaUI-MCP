# Technology Stack

**Analysis Date:** 2026-03-17

## Languages

**Primary:**
- C# 12.0 - Entire MCP server implementation, all business logic and protocols

**Secondary:**
- PowerShell - Used in CI/CD workflows for build and release automation
- YAML - GitHub Actions workflows and configuration

## Runtime

**Environment:**
- .NET 8.0 (Windows-specific target: `net8.0-windows`)
- Supports Windows 10/11, multi-architecture (x64 and ARM64)

**Package Manager:**
- NuGet - Package management via .NET CLI
- Lockfile: Present (`packages.lock.json` auto-generated during restore)

## Frameworks

**Core:**
- ASP.NET Core - Used for HTTP/SSE transport via `Microsoft.AspNetCore.Builder` and `Microsoft.AspNetCore.Hosting` (referenced as FrameworkReference, not NuGet package)

**UI Automation:**
- FlaUI.Core 5.0.0 - Windows UI Automation abstraction layer for element discovery and interaction
- FlaUI.UIA3 5.0.0 - UI Automation 3 provider for modern Windows apps (WPF, UWP, Win32)

**Build/Dev:**
- GitVersion 5.x - Semantic versioning from git history (CI/CD only, not a runtime dependency)

## Key Dependencies

**Critical:**
- System.Drawing.Common 10.0.2 - Screenshot capture and image encoding (PNG)
- System.Text.Json 10.0.2 - JSON serialization for MCP protocol messages (JSON-RPC 2.0 over stdio and HTTP)

**Infrastructure:**
- None - No external API clients, databases, or cloud SDKs

## Configuration

**Environment:**
- Command-line arguments only: `--transport` (stdio|sse), `--port` (default 8080 for SSE)
- No configuration files required at runtime
- No environment variables referenced

**Build:**
- Project file: `src/FlaUI.Mcp/FlaUI.Mcp.csproj`
- Version configuration: `GitVersion.yml` - Continuous Deployment mode with semantic versioning from git tags
- Enable implicit usings and nullable reference types in project settings

## Platform Requirements

**Development:**
- Windows 10/11 (required for UI Automation APIs)
- .NET 8.0 SDK (minimum for building)

**Production:**
- Deployment target: Windows (x64 and ARM64 architectures)
- Standalone or framework-dependent binaries available
- No Linux/macOS support (Windows UI Automation APIs not available on other platforms)

## Build & Publish

**Build Configuration:**
- Target: `Release` for production builds
- Self-contained option: `PublishSingleFile=true` with `IncludeNativeLibrariesForSelfExtract=true`
- Framework-dependent option: Requires .NET 8.0 runtime to be pre-installed

**Architectures Supported:**
- `win-x64` - 64-bit Windows
- `win-arm64` - ARM64 Windows (e.g., Surface Pro X)

---

*Stack analysis: 2026-03-17*
