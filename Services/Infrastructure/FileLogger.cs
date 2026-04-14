using System.IO;
using System.Text;
using System.Diagnostics;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services.Infrastructure;

public class FileLogger : ILogger
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public FileLogger()
    {
        var logsDir = AppPaths.GetLogsDirectory();
        var fileName = $"log_{DateTime.Now:yyyyMMdd}.txt";
        _logFilePath = Path.Combine(logsDir, fileName);
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] {message}";
        
        if (exception != null)
        {
            logEntry += Environment.NewLine + exception.ToString();
        }

        var fullEntry = logEntry + Environment.NewLine;

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, fullEntry, Encoding.UTF8);
            }
            catch
            {
                // Fallback: try to write to a temporary file if the primary path is blocked
                try
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), $"gamepad_mapping_emergency_{DateTime.Now:yyyyMMdd}.log");
                    File.AppendAllText(tempPath, $"[EMERGENCY FALLBACK] {fullEntry}", Encoding.UTF8);
                }
                catch
                {
                    // Last resort: Debug output only
                    Debug.WriteLine($"LOG FAILURE: {fullEntry}");
                }
            }
        }
    }
}


