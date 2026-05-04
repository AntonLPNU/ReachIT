using System;
using System.IO;

namespace ReachIT.Infrastructure.Logging;

public interface ILocalLogger
{
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
}

public sealed class LocalLogger : ILocalLogger
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public LocalLogger()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDir = Path.Combine(appData, "ReachIT", "logs");
        Directory.CreateDirectory(logDir);

        var dateStr = DateTime.Now.ToString("yyyy-MM-dd");
        _logFilePath = Path.Combine(logDir, $"ReachIT_{dateStr}.log");
    }

    public void LogInformation(string message) => Log("INFO", message);

    public void LogWarning(string message) => Log("WARN", message);

    public void LogError(string message, Exception? ex = null)
    {
        Log("ERROR", ex != null ? $"{message}\n{ex}" : message);
    }

    private void Log(string level, string message)
    {
        try
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, logEntry);
            }
        }
        catch
        {
            // Fallback for logging failure - shouldn't crash app
        }
    }
}
