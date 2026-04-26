namespace FlaUI.Mcp;

/// <summary>
/// Parsed command-line options for FlaUI-MCP. Pure value record — extracted from Program.cs
/// so transport-default and bind-address parsing can be unit-tested without spinning up Kestrel.
/// </summary>
public sealed record CliOptions(
    bool Silent,
    bool Debug,
    bool Install,
    bool Uninstall,
    bool Console,
    bool Task,
    bool RemoveTask,
    bool Help,
    string Transport,
    int Port,
    string BindAddress)
{
    /// <summary>
    /// Default options used when no CLI args are supplied. Per HTTP-08, default transport is "http".
    /// Per HTTP-06, default bind address is loopback (127.0.0.1).
    /// </summary>
    public static CliOptions Default => new CliOptions(
        Silent: false,
        Debug: false,
        Install: false,
        Uninstall: false,
        Console: false,
        Task: false,
        RemoveTask: false,
        Help: false,
        Transport: "http",
        Port: 3020,
        BindAddress: "127.0.0.1");

    /// <summary>
    /// Parse command-line arguments. Mirrors the legacy switch in Program.cs exactly for existing
    /// flags and adds <c>--bind &lt;address&gt;</c> handling. Help printing is left to Program.cs
    /// (this method only sets <see cref="Help"/>=true).
    /// </summary>
    public static CliOptions Parse(string[] args)
    {
        var silent = false;
        var debug = false;
        var install = false;
        var uninstall = false;
        var console = false;
        var task = false;
        var removeTask = false;
        var help = false;
        var transport = Default.Transport;
        var port = Default.Port;
        var bindAddress = Default.BindAddress;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--install" or "-i":
                    install = true;
                    break;
                case "--uninstall" or "-u":
                    uninstall = true;
                    break;
                case "--silent" or "-s":
                    silent = true;
                    break;
                case "--debug" or "-d":
                    debug = true;
                    break;
                case "--console" or "-c":
                    console = true;
                    break;
                case "--task":
                    task = true;
                    break;
                case "--removetask":
                    removeTask = true;
                    break;
                case "--transport" when i + 1 < args.Length:
                    transport = args[++i].ToLowerInvariant();
                    break;
                case "--port" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var p)) port = p;
                    break;
                case "--bind" when i + 1 < args.Length:
                    bindAddress = args[++i];
                    break;
                case "--help" or "-?":
                    help = true;
                    break;
            }
        }

        return new CliOptions(
            Silent: silent,
            Debug: debug,
            Install: install,
            Uninstall: uninstall,
            Console: console,
            Task: task,
            RemoveTask: removeTask,
            Help: help,
            Transport: transport,
            Port: port,
            BindAddress: bindAddress);
    }
}
