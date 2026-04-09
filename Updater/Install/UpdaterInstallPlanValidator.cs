using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Updater.Install;

internal static class UpdaterInstallPlanValidator
{
    public static void Validate(InstallExecutionPlanDto plan)
    {
        if (string.IsNullOrWhiteSpace(plan.ExpectedZipSha256) || plan.ExpectedZipSha256.Length != 64 || !plan.ExpectedZipSha256.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Expected package SHA-256 format is invalid.");
        if (!File.Exists(plan.ZipPackagePath))
            throw new FileNotFoundException("Update package ZIP file does not exist.", plan.ZipPackagePath);
        if (!Directory.Exists(plan.TargetDirectoryPath))
            throw new DirectoryNotFoundException($"Target directory does not exist: {plan.TargetDirectoryPath}");
        if (string.IsNullOrWhiteSpace(plan.AppExecutablePath))
            throw new InvalidOperationException("App executable path is required.");
        if (!Path.IsPathFullyQualified(plan.TargetDirectoryPath) || !Path.IsPathFullyQualified(plan.ZipPackagePath) || !Path.IsPathFullyQualified(plan.AppExecutablePath))
            throw new InvalidOperationException("Install plan paths must be fully qualified.");

        var targetDir = Path.GetFullPath(plan.TargetDirectoryPath);
        var appExePath = Path.GetFullPath(plan.AppExecutablePath);
        var zipPath = Path.GetFullPath(plan.ZipPackagePath);
        var updatesDir = Path.Combine(targetDir, "Updates");
        var logsDir = Path.Combine(targetDir, "Logs");

        if (!UpdaterInstallPathPolicy.IsSubPathOf(appExePath, targetDir))
            throw new InvalidOperationException("App executable path must be inside target directory.");
        if (!UpdaterInstallPathPolicy.IsSubPathOf(zipPath, updatesDir))
            throw new InvalidOperationException("ZIP package path must be inside target Updates directory.");
        if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ZIP package path must end with .zip.");

        if (!string.IsNullOrWhiteSpace(plan.InstallLogPath))
        {
            var logPath = Path.GetFullPath(plan.InstallLogPath);
            if (!UpdaterInstallPathPolicy.IsSubPathOf(logPath, logsDir))
                throw new InvalidOperationException("Install log path must be inside target Logs directory.");
        }

        if (plan.ProcessIdToWaitFor <= 0)
            throw new InvalidOperationException("Invalid installer caller process id.");
        if (!IsMatchingCallerProcessOrExited(plan.ProcessIdToWaitFor, appExePath))
            throw new InvalidOperationException("Installer caller process verification failed.");

        var marker = Path.Combine(targetDir, "Assets", "Config", "default_settings.json");
        if (!File.Exists(marker))
            throw new InvalidOperationException("Target directory is not recognized as a valid Gamepad Mapping installation.");
    }

    private static bool IsMatchingCallerProcessOrExited(int processId, string expectedProcessPath)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var actualPath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(actualPath))
                return false;
            return string.Equals(
                Path.GetFullPath(actualPath),
                Path.GetFullPath(expectedProcessPath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            // Caller process already exited before updater validation runs.
            // This is expected in the normal handoff flow.
            return true;
        }
        catch
        {
            return false;
        }
    }
}
