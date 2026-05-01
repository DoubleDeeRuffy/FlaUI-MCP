namespace FlaUI.Mcp;

/// <summary>
/// POCO bound from the "Server" section of appsettings.json / FLAUI_MCP_ env vars.
/// Each property is null when the corresponding key is absent at every layer.
/// </summary>
public sealed record ServerConfig(string? BindAddress, int? Port);

/// <summary>
/// What the user actually typed on the CLI. Null = flag absent (so CLI presence
/// is <c>field is not null</c>, not <c>field != default</c>). Used by <see cref="ConfigResolver"/>
/// to give CLI strict precedence over file + env layers per CONTEXT.md.
/// </summary>
public sealed record CliOverrides(string? BindAddress, int? Port);
