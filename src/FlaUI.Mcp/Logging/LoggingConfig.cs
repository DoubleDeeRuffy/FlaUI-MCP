using NLog;
using NLog.Config;
using NLog.Targets;

namespace FlaUI.Mcp.Logging;

/// <summary>
/// Programmatic NLog configuration. Call CleanOldLogfiles() before ConfigureLogging().
/// </summary>
public static class LoggingConfig
{
    /// <summary>
    /// App-local log directory. All log files are written here.
    /// </summary>
    public static string LogDirectory => Path.Combine(AppContext.BaseDirectory, "Log");

    /// <summary>
    /// Configures NLog with file and optional console targets.
    /// </summary>
    /// <param name="debug">If true, activates Debug.log target at Debug level.</param>
    /// <param name="logDirectory">Directory where log files are written.</param>
    /// <param name="enableConsoleTarget">If true, adds console target (SSE mode only — must be false in stdio mode).</param>
    public static void ConfigureLogging(bool debug, string logDirectory, bool enableConsoleTarget)
    {
        var fileLayout = "${longdate} | ${pad:padding=5:inner=${level:uppercase=true}} | ${callsite} | ${message} ${exception:format=tostring}";
        var consoleLayout = "${time} | ${pad:padding=-5:inner=${level:uppercase=true}} | ${pad:padding=-80:inner=${replace:inner=${callsite}:searchFor=FlaUI\\.Mcp\\.:replaceWith=:regex=true}} | ${message} ${exception:format=tostring}";

        LogManager.Setup().LoadConfiguration(c =>
        {
            // Error.log — always active, captures Error and above
            var errorTarget = new FileTarget("errorFile")
            {
                FileName = Path.Combine(logDirectory, "Error.log"),
                Layout = fileLayout
            };
            c.ForLogger().FilterMinLevel(LogLevel.Error).WriteTo(errorTarget).WithAsync();

            // Debug.log — only when -debug flag is set
            if (debug)
            {
                var debugTarget = new FileTarget("debugFile")
                {
                    FileName = Path.Combine(logDirectory, "Debug.log"),
                    Layout = fileLayout
                };
                c.ForLogger().FilterMinLevel(LogLevel.Debug).WriteTo(debugTarget).WithAsync();
            }

            // Console target — only in SSE transport mode (stdout must be clean in stdio mode)
            if (enableConsoleTarget)
            {
                var consoleTarget = new ConsoleTarget("console")
                {
                    Layout = consoleLayout
                };
                var consoleMinLevel = debug ? LogLevel.Debug : LogLevel.Info;
                c.ForLogger().FilterMinLevel(consoleMinLevel).WriteTo(consoleTarget).WithAsync();
            }

            // Suppress verbose framework logs
            c.ForLogger("System.*").WriteToNil(LogLevel.Warn);
            c.ForLogger("Microsoft.*").WriteToNil(LogLevel.Warn);
            c.ForLogger("Microsoft.Hosting.Lifetime*").WriteToNil(LogLevel.Info);
        });
    }
}
