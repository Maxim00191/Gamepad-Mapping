using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.ComponentModel;
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
        EnsureSecureDirectory(planDirectory);
        var planPath = Path.Combine(planDirectory, $"install-plan-{Guid.NewGuid():N}.json");
        
        var appDisplayName = "GamepadMapping"; // Could be moved to a configuration service if needed
        var handshakeName = $"Global\\{appDisplayName}-Install-{Guid.NewGuid():N}";

        var preserveNames = (request.PreserveDirectoryNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().Replace('/', '\\').Trim('\\'))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var currentProcess = Process.GetCurrentProcess();
        var plan = new UpdateInstallExecutionPlan(
            ZipPackagePath: Path.GetFullPath(request.ZipPackagePath),
            TargetDirectoryPath: Path.GetFullPath(request.TargetDirectoryPath),
            AppExecutablePath: Path.GetFullPath(request.AppExecutablePath),
            AppDisplayName: appDisplayName,
            PreserveDirectoryNames: preserveNames,
            ProcessIdToWaitFor: currentProcess.Id,
            ProcessStartTimeUtc: currentProcess.StartTime.ToUniversalTime(),
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
        
        var fullUpdaterPath = Path.GetFullPath(updaterPath);
        var fullPlanPath = Path.GetFullPath(planPath);

        var psi = new ProcessStartInfo
        {
            FileName = fullUpdaterPath,
            Arguments = $"--plan \"{fullPlanPath}\" --handshake \"{handshakeName}\"",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = needsElevation,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal
        };

        if (needsElevation)
        {
            psi.Verb = "runas";
        }

        try
        {
            // Use a named mutex for a secure, atomic handshake.
            // Initially owned by us; the updater will release it to signal readiness.
            using var handshakeMutex = new Mutex(true, handshakeName, out _);

            var process = Process.Start(psi);
            if (process is null)
                return new UpdateInstallExecutionResult(false, "Failed to start updater process (Process.Start returned null).");

            var result = WaitForUpdaterHandshake(process, handshakeMutex, timeoutMs: 20000);
            return result.Succeeded
                ? new UpdateInstallExecutionResult(true)
                : new UpdateInstallExecutionResult(false, result.ErrorMessage);
        }
        catch (Win32Exception ex)
        {
            if (ex.NativeErrorCode == 1223)
                return new UpdateInstallExecutionResult(false, "Update installation was canceled at the UAC prompt.");
            return new UpdateInstallExecutionResult(false, $"System error launching updater (0x{ex.NativeErrorCode:X}): {ex.Message}");
        }
        catch (Exception ex)
        {
            return new UpdateInstallExecutionResult(false, $"Failed to launch updater: {ex.GetType().Name} - {ex.Message}");
        }
    }

    private static void EnsureSecureDirectory(string path)
    {
        if (Directory.Exists(path)) return;

        // On Windows, we can set ACLs to restrict access to the current user and SYSTEM.
        // For simplicity and cross-platform compatibility (though this app is WPF), 
        // we'll ensure the directory is created. In a full production setup, 
        // you'd use FileSystemSecurity to lock this down.
        Directory.CreateDirectory(path);
    }

    private static string ResolveUpdaterExecutablePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDirectory, "Updater.exe");
        if (File.Exists(candidate))
            return candidate;

#if DEBUG
        // Fallback for developer runs where output copy may be missing.
        var rootCandidate = Path.Combine(AppPaths.ResolveContentRoot(), "Updater.exe");
        if (File.Exists(rootCandidate))
            return rootCandidate;
#endif

        return candidate;
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

    private static (bool Succeeded, string? ErrorMessage) WaitForUpdaterHandshake(Process process, Mutex handshakeMutex, int timeoutMs)
    {
        try
        {
            // Wait for the mutex to be released by the updater (signaling it's ready)
            // or for the process to exit prematurely.
            var waitHandles = new WaitHandle[] { handshakeMutex };
            
            var deadlineUtc = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadlineUtc)
            {
                // We use a short timeout in WaitAny to periodically check if the process has exited.
                var signaledIndex = WaitHandle.WaitAny(waitHandles, 200);
                
                if (signaledIndex == 0)
                {
                    // Mutex was released or abandoned (either way, the updater reached the handshake point).
                    return (true, null);
                }

                if (process.HasExited)
                {
                    if (process.ExitCode == 3)
                        return (false, "Updater rejected an invalid or expired install request. Please re-download the update package and try again.");
                    
                    // If the process exited with 0 but didn't release the mutex, it might be the bootstrap process
                    // that successfully launched the relocated one. We should keep waiting unless it's a non-zero exit.
                    if (process.ExitCode != 0)
                        return (false, $"Updater exited before startup handshake (exit code {process.ExitCode}).");
                }
            }

            return (false, "Updater startup handshake timed out before confirmation.");
        }
        catch (AbandonedMutexException)
        {
            // This is actually a success in our case: the updater process that held the mutex exited,
            // which happens when the bootstrap process finishes after the relocated one is ready.
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Error during updater handshake: {ex.Message}");
        }
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
