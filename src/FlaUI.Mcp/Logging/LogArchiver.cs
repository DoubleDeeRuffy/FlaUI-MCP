using System.IO.Compression;

namespace FlaUI.Mcp.Logging;

/// <summary>
/// Handles log archive rotation on startup. Must be called before ConfigureLogging()
/// to ensure each session starts with fresh log files.
/// </summary>
public static class LogArchiver
{
    private const int MaxZipFiles = 10;

    /// <summary>
    /// Archives existing .log files into a timestamped zip, enforces max 10 zip files.
    /// Creates the log directory if it does not exist.
    /// </summary>
    /// <param name="logDirectory">Path to the log directory.</param>
    public static void CleanOldLogfiles(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
            return;
        }

        // Rotate zip archives: keep newest MaxZipFiles, delete older ones
        var zipFiles = Directory.GetFiles(logDirectory, "*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        if (zipFiles.Count > MaxZipFiles)
        {
            foreach (var oldZip in zipFiles.Skip(MaxZipFiles))
            {
                oldZip.Delete();
            }
        }

        // Archive existing .log files into a new timestamped zip
        var logFiles = Directory.GetFiles(logDirectory, "*.log");
        if (logFiles.Length == 0)
            return;

        var tempDir = Path.Combine(logDirectory, "_archive_temp");
        try
        {
            Directory.CreateDirectory(tempDir);

            foreach (var logFile in logFiles)
            {
                var fileName = Path.GetFileName(logFile);
                File.Move(logFile, Path.Combine(tempDir, fileName));
            }

            var zipPath = Path.Combine(logDirectory, $"Logs-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");
            ZipFile.CreateFromDirectory(tempDir, zipPath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
