using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Updater.Install;

internal static class UpdaterInstallPlanValidator
{
    private static readonly TimeSpan ExitedCallerGraceWindow = TimeSpan.FromSeconds(120);

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

        // Priority 5: MAX_PATH (260 chars) Validation
        // The updater creates staging and backup directories with long suffixes (e.g., ".staging-32charsguid").
        // We must ensure the base path is short enough to accommodate these.
        // ".staging-00000000000000000000000000000000" is 41 chars.
        // We add a safety margin for nested files within the package.
        const int MaxPathLimit = 260;
        const int SafetyMargin = 60; // Accommodate .staging-GUID suffix + some nesting
        if (targetDir.Length > MaxPathLimit - SafetyMargin)
        {
            throw new InvalidOperationException(
                $"The installation path is too long ({targetDir.Length} chars). " +
                $"To ensure update reliability, please move {plan.AppDisplayName} to a shorter directory path (e.g., C:\\Games\\).");
        }

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
        if (!IsMatchingCallerProcessOrExitedWithinGraceWindow(plan.ProcessIdToWaitFor, plan.ProcessStartTimeUtc, appExePath, plan.CreatedAtUtc, plan.AppDisplayName))
            throw new InvalidOperationException("Installer caller process verification failed.");

        var marker = Path.Combine(targetDir, "Assets", "Config", "default_settings.json");
        if (!File.Exists(marker))
            throw new InvalidOperationException($"Target directory is not recognized as a valid {plan.AppDisplayName} installation.");
    }

    private static bool VerifyDigitalSignature(string filePath, string appDisplayName)
    {
        // For self-signed certificates, we verify that:
        // 1. The file has a valid Authenticode signature (WinVerifyTrust).
        // 2. The certificate used for signing matches our expected thumbprint.

        var fileInfo = new UpdaterInstallNativeMethods.WINTRUST_FILE_INFO(filePath);
        var data = new UpdaterInstallNativeMethods.WINTRUST_DATA(IntPtr.Zero);

        IntPtr filePtr = IntPtr.Zero;
        IntPtr dataPtr = IntPtr.Zero;

        try
        {
            filePtr = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
            Marshal.StructureToPtr(fileInfo, filePtr, false);

            data.pFile = filePtr; // Link file info to data structure
            dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(data));
            Marshal.StructureToPtr(data, dataPtr, false);

            int result = UpdaterInstallNativeMethods.WinVerifyTrust(
                IntPtr.Zero,
                UpdaterInstallNativeMethods.WINTRUST_ACTION_GENERIC_VERIFY_V2,
                dataPtr);

            // 0 = Success (Trusted root)
            // 0x800B0109 = Untrusted root (Expected for self-signed)
            if (result != 0 && (uint)result != 0x800B0109)
            {
                return false;
            }

            // Verify the specific thumbprint to prevent spoofing with a different self-signed cert
            using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(filePath);
            
            // This is your unique identity. If you re-generate your cert, update this value.
            const string ExpectedThumbprint = "B24744482F4EA296BB1CBD1DE4E7CCAF0607199A";
            return string.Equals(cert.Thumbprint, ExpectedThumbprint, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (filePtr != IntPtr.Zero) Marshal.FreeHGlobal(filePtr);
            if (dataPtr != IntPtr.Zero) Marshal.FreeHGlobal(dataPtr);
        }
    }

    private static bool IsMatchingCallerProcessOrExitedWithinGraceWindow(int processId, DateTimeOffset expectedStartTime, string expectedProcessPath, DateTimeOffset createdAtUtc, string appDisplayName)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var actualPath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(actualPath) || !File.Exists(actualPath))
            {
                return false;
            }

            var actualStartTime = process.StartTime.ToUniversalTime();
            // Allow 1 second tolerance for potential precision differences in process start time reporting
            var startTimeDiff = Math.Abs((actualStartTime - expectedStartTime.UtcDateTime).TotalSeconds);

            if (startTimeDiff > 1.0)
                throw new InvalidOperationException($"{appDisplayName} caller process identity mismatch (PID reuse detected).");

            var normalizedActualPath = Path.GetFullPath(actualPath);
            if (!string.Equals(normalizedActualPath, Path.GetFullPath(expectedProcessPath), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (!VerifyDigitalSignature(normalizedActualPath, appDisplayName))
            {
                throw new InvalidOperationException($"{appDisplayName} caller process digital signature verification failed.");
            }
            
            return true;
        }
        catch (ArgumentException)
        {
            // Caller process already exited before updater validation runs.
            // Allow only a short handoff grace window to reduce replay risk.
            var age = DateTimeOffset.UtcNow - createdAtUtc;
            if (age >= TimeSpan.Zero && age <= ExitedCallerGraceWindow)
                return true;

            throw new InvalidOperationException("Install request expired before handoff confirmation. Please re-download the update package and try again.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
