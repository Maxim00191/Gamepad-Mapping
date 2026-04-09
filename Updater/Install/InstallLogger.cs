using System;
using System.IO;
using System.Text;

namespace Updater.Install;

internal sealed class InstallLogger
{
    private readonly string _logPath;
    private readonly string _emergencyLogPath;
    private readonly string? _initialSecurityNotice;

    public InstallLogger(string preferredLogPath, string targetDirectoryPath)
    {
        (_logPath, _initialSecurityNotice) = ResolveLogPath(preferredLogPath, targetDirectoryPath);
        _emergencyLogPath = BuildEmergencyLogPath();
        if (!string.IsNullOrWhiteSpace(_initialSecurityNotice))
            Write("WARN", _initialSecurityNotice);
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        try
        {
            File.AppendAllText(_logPath, line + Environment.NewLine, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            WriteEmergency(line, ex);
        }
    }

    private void WriteEmergency(string originalLine, Exception originalError)
    {
        try
        {
            var emergencyLine =
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [WARN] Primary log write failed. LogPath={_logPath}. Error={originalError.GetType().Name}: {originalError.Message}";
            File.AppendAllText(
                _emergencyLogPath,
                emergencyLine + Environment.NewLine + originalLine + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch
        {
            try
            {
                Console.Error.WriteLine($"[Updater] Logging failure. Primary={_logPath}; Emergency={_emergencyLogPath}; Error={originalError.Message}");
                Console.Error.WriteLine(originalLine);
            }
            catch
            {
                // Final fallback: ignore.
            }
        }
    }

    private static (string LogPath, string? SecurityNotice) ResolveLogPath(string preferredLogPath, string targetDirectoryPath)
    {
        var trustedLogsRoot = BuildTrustedLogsRoot(targetDirectoryPath);

        try
        {
            if (!string.IsNullOrWhiteSpace(preferredLogPath))
            {
                var full = Path.GetFullPath(preferredLogPath);
                if (IsSubPathOf(full, trustedLogsRoot))
                {
                    var dir = Path.GetDirectoryName(full);
                    if (!string.IsNullOrWhiteSpace(dir))
                        Directory.CreateDirectory(dir);
                    return (full, null);
                }

                var notice = $"Rejected untrusted InstallLogPath: {full}. Allowed root: {trustedLogsRoot}. Using trusted fallback path.";
                Directory.CreateDirectory(trustedLogsRoot);
                var fallbackFromRejected = Path.Combine(trustedLogsRoot, $"update-install-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
                return (fallbackFromRejected, notice);
            }
        }
        catch
        {
        }

        Directory.CreateDirectory(trustedLogsRoot);
        var fallback = Path.Combine(trustedLogsRoot, $"update-install-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
        return (fallback, null);
    }

    private static string BuildTrustedLogsRoot(string targetDirectoryPath)
    {
        var targetRoot = Path.GetFullPath(targetDirectoryPath);
        return Path.Combine(targetRoot, "Logs");
    }

    private static bool IsSubPathOf(string candidatePath, string rootPath)
    {
        var candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildEmergencyLogPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GamepadMapping", "UpdaterLogs");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"updater-emergency-{DateTimeOffset.Now:yyyyMMdd}.log");
    }
}
