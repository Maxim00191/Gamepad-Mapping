using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Updater.Install;

internal static class UpdaterInstallRecovery
{
    public static void WriteEmergencyRecoveryHint(string targetDir, string backupDir, InstallLogger logger)
    {
        var message =
            $"CRITICAL: Automatic rollback failed. Application target directory may be missing.\n" +
            $"Target directory: {targetDir}\n" +
            $"Backup directory: {backupDir}\n" +
            $"Manual recovery: close related processes, reboot if files are locked, then rename/move backup directory back to target directory path.";

        logger.Error(message);

        try
        {
            var notePath = Path.Combine(
                Directory.GetParent(backupDir)?.FullName ?? Path.GetTempPath(),
                "GamepadMapping-RECOVERY-INSTRUCTIONS.txt");
            File.WriteAllText(notePath, message, new UTF8Encoding(false));
            logger.Error($"Recovery instructions file written: {notePath}");
            TryOpenRecoveryInstructions(notePath, logger);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to write recovery instructions file: {ex.Message}");
        }
    }

    private static void TryOpenRecoveryInstructions(string notePath, InstallLogger logger)
    {
        try
        {
            var notepadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");
            if (File.Exists(notepadPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = notepadPath,
                    UseShellExecute = false,
                    ArgumentList = { notePath }
                });
                logger.Error($"Opened recovery instructions in Notepad: {notePath}");
                return;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to open recovery instructions in Notepad: {ex.Message}");
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = notePath, UseShellExecute = true });
            logger.Error($"Opened recovery instructions with default app: {notePath}");
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to auto-open recovery instructions: {ex.Message}");
        }
    }
}
