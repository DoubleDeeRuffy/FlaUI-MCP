using Microsoft.Extensions.Configuration;

namespace FlaUI.Mcp;

/// <summary>
/// Pure-function resolver that layers configuration sources for the Kestrel bind address and port.
/// Precedence (lowest → highest): defaults &lt; appsettings.json &lt; FLAUI_MCP_ env vars &lt; CLI flags.
///
/// Designed to be unit-testable without spinning up Kestrel, without writing to
/// <see cref="System.AppContext.BaseDirectory"/>, and without setting real environment variables —
/// callers (tests) inject the file path and env dict explicitly.
/// </summary>
public static class ConfigResolver
{
    /// <summary>
    /// Resolve the effective bind address + port by layering sources in precedence order.
    /// </summary>
    /// <param name="defaults">Hardcoded defaults (typically <see cref="CliOptions.Default"/>).</param>
    /// <param name="appsettingsPath">Absolute path to appsettings.json. Pass null or empty to skip the file layer entirely. When non-null, the file is loaded with <c>optional: true</c> so a missing file falls through silently.</param>
    /// <param name="envOverride">Test-injectable env dict with already-translated keys (e.g. "Server:Port"). Pass <c>null</c> in production to read real env vars with prefix <c>FLAUI_MCP_</c>.</param>
    /// <param name="cli">CLI overrides — only non-null fields are applied. CLI presence is detected by <c>field is not null</c>, not by value comparison against defaults, so passing <c>--bind 127.0.0.1</c> still beats an appsettings.json value of <c>0.0.0.0</c>.</param>
    /// <returns>Tuple of the resolved (BindAddress, Port).</returns>
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

        // Per-key reads (not GetSection<T>()) — sidesteps the empty-section-yields-default-POCO trap
        // and gives clean per-key precedence.
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
