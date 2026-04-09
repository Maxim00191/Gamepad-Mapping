using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

namespace Updater.Install;

internal static class UpdaterInstallProcessOps
{
    public static void WaitForParentExit(int processIdToWaitFor)
    {
        if (processIdToWaitFor <= 0)
            return;
        try
        {
            using var process = Process.GetProcessById(processIdToWaitFor);
            process.WaitForExit();
        }
        catch
        {
        }
    }

    public static void EnsureUpdaterRunsOutsideTargetDirectory(string targetDirectory)
    {
        var updaterPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(updaterPath))
            throw new InvalidOperationException("Cannot resolve updater executable path.");

        var updaterDir = Path.GetDirectoryName(Path.GetFullPath(updaterPath));
        if (string.IsNullOrWhiteSpace(updaterDir))
            throw new InvalidOperationException("Cannot resolve updater executable directory.");

        if (UpdaterInstallPathPolicy.IsSubPathOf(updaterDir, targetDirectory))
            throw new InvalidOperationException($"Unsafe updater location detected. Updater must run outside target directory. UpdaterDir={updaterDir}; TargetDir={Path.GetFullPath(targetDirectory)}");
    }

    public static void StopAppProcessesByPath(string executablePath, int excludedProcessId, InstallLogger logger)
    {
        var targetFullPath = Path.GetFullPath(executablePath);
        var targetName = Path.GetFileNameWithoutExtension(targetFullPath);
        var matching = Process.GetProcessesByName(targetName);
        foreach (var process in matching)
        {
            if (process.Id == excludedProcessId)
            {
                process.Dispose();
                continue;
            }

            try
            {
                var processPath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(processPath) &&
                    !string.Equals(Path.GetFullPath(processPath), targetFullPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                logger.Info($"Stopping process {process.ProcessName} (PID={process.Id}).");
                process.Kill(entireProcessTree: false);
                process.WaitForExit(5000);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    public static void WaitForProcessDrainByPath(string executablePath, InstallLogger logger, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + Math.Max(1000, timeoutMs);
        var targetFullPath = Path.GetFullPath(executablePath);
        var targetName = Path.GetFileNameWithoutExtension(targetFullPath);

        while (Environment.TickCount64 < deadline)
        {
            var stillRunning = false;
            foreach (var process in Process.GetProcessesByName(targetName))
            {
                try
                {
                    var processPath = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(processPath) ||
                        string.Equals(Path.GetFullPath(processPath), targetFullPath, StringComparison.OrdinalIgnoreCase))
                        stillRunning = true;
                }
                catch
                {
                    stillRunning = true;
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (!stillRunning)
                return;
            Thread.Sleep(120);
        }

        logger.Error($"Process drain timeout reached before swap. Target executable may still be running: {targetFullPath}");
    }

    public static void RestartApplication(string targetDir, string appExeRelativePath, InstallLogger logger)
    {
        var appPath = Path.GetFullPath(Path.Combine(targetDir, appExeRelativePath));
        if (!File.Exists(appPath))
        {
            logger.Error($"Skip restart because executable not found: {appPath}");
            return;
        }

        try
        {
            if (IsCurrentProcessElevated())
            {
                if (TryStartUnelevatedViaActiveSessionToken(appPath, logger))
                {
                    logger.Info($"Application restarted unelevated via active user token: {appPath}");
                    return;
                }

                logger.Error("Failed to restart application unelevated in elevated context. For security, auto-restart is skipped. Please restart the application manually.");
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = appPath, UseShellExecute = true });
            logger.Info($"Application restarted: {appPath}");
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to restart application: {ex.Message}");
        }
    }

    private static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool TryStartUnelevatedViaActiveSessionToken(string appPath, InstallLogger logger)
    {
        try
        {
            var sessionId = UpdaterInstallNativeMethods.WTSGetActiveConsoleSessionId();
            if (sessionId == uint.MaxValue)
                return false;
            if (!UpdaterInstallNativeMethods.WTSQueryUserToken(sessionId, out var rawUserToken))
                return false;

            try
            {
                if (!UpdaterInstallNativeMethods.DuplicateTokenEx(
                        rawUserToken,
                        UpdaterInstallNativeMethods.TOKEN_ASSIGN_PRIMARY | UpdaterInstallNativeMethods.TOKEN_DUPLICATE | UpdaterInstallNativeMethods.TOKEN_QUERY | UpdaterInstallNativeMethods.TOKEN_ADJUST_DEFAULT | UpdaterInstallNativeMethods.TOKEN_ADJUST_SESSIONID,
                        IntPtr.Zero,
                        UpdaterInstallNativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                        UpdaterInstallNativeMethods.TOKEN_TYPE.TokenPrimary,
                        out var primaryToken))
                {
                    return false;
                }

                try
                {
                    var startup = new UpdaterInstallNativeMethods.STARTUPINFO
                    {
                        cb = Marshal.SizeOf<UpdaterInstallNativeMethods.STARTUPINFO>(),
                        lpDesktop = @"winsta0\default"
                    };

                    if (UpdaterInstallNativeMethods.CreateProcessAsUser(
                            primaryToken,
                            appPath,
                            null,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            UpdaterInstallNativeMethods.CREATE_UNICODE_ENVIRONMENT,
                            IntPtr.Zero,
                            null,
                            ref startup,
                            out var processInfo))
                    {
                        UpdaterInstallNativeMethods.CloseHandle(processInfo.hThread);
                        UpdaterInstallNativeMethods.CloseHandle(processInfo.hProcess);
                        return true;
                    }

                    logger.Error($"CreateProcessAsUser failed. Win32Error={Marshal.GetLastWin32Error()}");
                    return false;
                }
                finally
                {
                    UpdaterInstallNativeMethods.CloseHandle(primaryToken);
                }
            }
            finally
            {
                UpdaterInstallNativeMethods.CloseHandle(rawUserToken);
            }
        }
        catch
        {
            return false;
        }
    }
}
