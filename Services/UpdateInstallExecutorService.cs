using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services;

public sealed class UpdateInstallExecutorService : IUpdateInstallExecutorService
{
    public UpdateInstallExecutionResult Execute(UpdateInstallRequest request)
    {
        if (request is null)
            return new UpdateInstallExecutionResult(false, "Installer request is required.");

        if (string.IsNullOrWhiteSpace(request.ZipPackagePath) || !File.Exists(request.ZipPackagePath))
            return new UpdateInstallExecutionResult(false, "Update package ZIP file does not exist.");

        if (string.IsNullOrWhiteSpace(request.TargetDirectoryPath) || !Directory.Exists(request.TargetDirectoryPath))
            return new UpdateInstallExecutionResult(false, "Target install directory does not exist.");

        if (string.IsNullOrWhiteSpace(request.AppExecutablePath))
            return new UpdateInstallExecutionResult(false, "App executable path is required.");

        if (!VerifyPackageHash(request.ZipPackagePath, request.ExpectedZipSha256, out var hashError))
            return new UpdateInstallExecutionResult(false, hashError);

        var planDirectory = Path.Combine(AppPaths.GetUpdateDownloadsDirectory(), "install-plans");
        Directory.CreateDirectory(planDirectory);
        var planPath = Path.Combine(planDirectory, $"install-plan-{Guid.NewGuid():N}.json");
        var ackPath = Path.Combine(planDirectory, $"install-ack-{Guid.NewGuid():N}.txt");

        var preserveNames = (request.PreserveDirectoryNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().Replace('/', '\\').Trim('\\'))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var plan = new UpdateInstallExecutionPlan(
            ZipPackagePath: Path.GetFullPath(request.ZipPackagePath),
            TargetDirectoryPath: Path.GetFullPath(request.TargetDirectoryPath),
            AppExecutablePath: Path.GetFullPath(request.AppExecutablePath),
            PreserveDirectoryNames: preserveNames,
            ProcessIdToWaitFor: request.ProcessIdToWaitFor,
            TrustedReleaseTag: request.TrustedReleaseTag?.Trim(),
            ExpectedZipSha256: request.ExpectedZipSha256!.Trim().ToLowerInvariant(),
            InstallLogPath: ResolveInstallLogPath(request.InstallLogPath),
            RemoveOrphanFiles: request.RemoveOrphanFiles,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(planPath, JsonSerializer.Serialize(plan, options));

        var updaterPath = ResolveUpdaterExecutablePath();
        if (!File.Exists(updaterPath))
            return new UpdateInstallExecutionResult(false, $"Updater executable not found: {updaterPath}");

        var needsElevation = !CanWriteToDirectory(request.TargetDirectoryPath);
        var psi = new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = $"--plan \"{planPath}\" --ack \"{ackPath}\"",
            UseShellExecute = true,
            Verb = needsElevation ? "runas" : "open",
            WorkingDirectory = request.TargetDirectoryPath,
            CreateNoWindow = false
        };

        try
        {
            TryDeleteFileIfExists(ackPath);
            var process = Process.Start(psi);
            if (process is null)
                return new UpdateInstallExecutionResult(false, "Failed to start updater process.");

            var handshake = WaitForUpdaterHandshake(process, ackPath, timeoutMs: 20000);
            return handshake.Succeeded
                ? new UpdateInstallExecutionResult(true)
                : new UpdateInstallExecutionResult(false, handshake.ErrorMessage);
        }
        catch (Exception ex)
        {
            return new UpdateInstallExecutionResult(false, $"Failed to launch updater: {ex.Message}");
        }
    }

    private static string ResolveUpdaterExecutablePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDirectory, "Updater.exe");
        if (File.Exists(candidate))
            return candidate;

        // Fallback for developer runs where output copy may be missing.
        var rootCandidate = Path.Combine(AppPaths.ResolveContentRoot(), "Updater.exe");
        return File.Exists(rootCandidate) ? rootCandidate : candidate;
    }

    private static string ResolveInstallLogPath(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
            return requestedPath;

        var logsDir = AppPaths.GetLogsDirectory();
        var fileName = $"update-install-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log";
        return Path.Combine(logsDir, fileName);
    }

    private static bool VerifyPackageHash(string zipPath, string? expectedSha256, out string? errorMessage)
    {
        errorMessage = null;
        var expected = expectedSha256?.Trim();
        if (string.IsNullOrWhiteSpace(expected))
        {
            errorMessage = "Package integrity check is required but checksum is missing.";
            return false;
        }

        if (expected.Length != 64 || !expected.All(Uri.IsHexDigit))
        {
            errorMessage = "Expected package SHA-256 format is invalid.";
            return false;
        }

        try
        {
            using var stream = File.OpenRead(zipPath);
            var hashBytes = SHA256.HashData(stream);
            var actual = Convert.ToHexString(hashBytes).ToLowerInvariant();
            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                return true;

            errorMessage = "Package integrity check failed (SHA-256 mismatch).";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to verify package hash: {ex.Message}";
            return false;
        }
    }

    private static bool CanWriteToDirectory(string targetDirectoryPath)
    {
        try
        {
            var probe = Path.Combine(targetDirectoryPath, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (bool Succeeded, string? ErrorMessage) WaitForUpdaterHandshake(Process process, string ackPath, int timeoutMs)
    {
        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadlineUtc)
        {
            if (File.Exists(ackPath))
                return (true, null);

            if (process.HasExited)
                return (false, $"Updater exited before startup handshake (exit code {process.ExitCode}).");

            Thread.Sleep(100);
        }

        return (false, "Updater startup handshake timed out before confirmation.");
    }

    private static void TryDeleteFileIfExists(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
