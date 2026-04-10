using System;
using System.IO;

namespace Updater.Install;

internal sealed class UpdaterInstallRunner
{
    public int Run(InstallExecutionPlanDto plan)
    {
        UpdaterInstallPlanValidator.Validate(plan);
        var logger = new InstallLogger(plan.InstallLogPath, plan.TargetDirectoryPath);
        logger.Info("Atomic updater started.");

        var targetDir = Path.GetFullPath(plan.TargetDirectoryPath);
        using var installationLock = UpdaterInstallLock.AcquireInstallationMutexOrThrow(targetDir);
        logger.Info($"Acquired global install lock for target: {targetDir}");

        UpdaterInstallProcessOps.WaitForParentExit(plan.ProcessIdToWaitFor);
        UpdaterInstallProcessOps.StopAppProcessesByPath(plan.AppExecutablePath, plan.ProcessIdToWaitFor, logger);
        UpdaterInstallProcessOps.EnsureUpdaterRunsOutsideTargetDirectory(targetDir);

        var targetParent = Directory.GetParent(targetDir)?.FullName
            ?? throw new InvalidOperationException("Target directory parent is missing.");
        var targetName = new DirectoryInfo(targetDir).Name;
        var operationId = Guid.NewGuid().ToString("N");
        var stagingRoot = Path.Combine(targetParent, $"{targetName}.staging-{operationId}");
        var oldRoot = Path.Combine(targetParent, $"{targetName}.backup-{operationId}");
        var preserveBackupRoot = Path.Combine(targetParent, $"{targetName}.preserve-{operationId}");
        var trustedZipPath = Path.Combine(targetParent, $"{targetName}.trusted-{operationId}.zip");

        var appExeRelativePath = Path.GetRelativePath(targetDir, Path.GetFullPath(plan.AppExecutablePath));
        var preserved = UpdaterInstallPathPolicy.NormalizePreservePaths(plan.PreserveDirectoryNames);
        var swapStarted = false;
        var rollbackFailed = false;

        try
        {
            logger.Info("Creating trusted ZIP snapshot.");
            UpdaterInstallFileOps.CopyAndVerifyZipSnapshot(plan.ZipPackagePath, trustedZipPath, plan.ExpectedZipSha256);
            logger.Info("Trusted ZIP snapshot created.");

            logger.Info("Extracting package.");
            var extractedPackageRoot = UpdaterInstallFileOps.ExtractPackageRoot(trustedZipPath, stagingRoot);

            if (!plan.RemoveOrphanFiles)
            {
                logger.Info("RemoveOrphanFiles=false, seeding staging with current target files.");
                UpdaterInstallFileOps.CopyDirectoryContents(targetDir, stagingRoot, overwriteFiles: true);
            }

            logger.Info("Applying package files into staging.");
            UpdaterInstallFileOps.CopyDirectoryContents(extractedPackageRoot, stagingRoot, overwriteFiles: true);
            UpdaterInstallFileOps.SafeDeleteDirectory(Path.Combine(stagingRoot, "_extract"), logger);

            logger.Info("Backing up preserved user data.");
            UpdaterInstallFileOps.BackupPreservedData(targetDir, preserveBackupRoot, preserved);
            logger.Info("Restoring preserved user data into staging.");
            UpdaterInstallFileOps.RestorePreservedData(preserveBackupRoot, stagingRoot);
            logger.Info("Merging config files with local values priority.");
            UpdaterInstallFileOps.MergeConfigFiles(targetDir, stagingRoot, logger);

            logger.Info("Pre-swap process sweep to close race window.");
            UpdaterInstallProcessOps.StopAppProcessesByPath(plan.AppExecutablePath, plan.ProcessIdToWaitFor, logger);
            UpdaterInstallProcessOps.WaitForProcessDrainByPath(plan.AppExecutablePath, logger, timeoutMs: 4000);

            logger.Info("Starting atomic directory swap.");
            UpdaterInstallFileOps.MoveDirectoryWithRetry(targetDir, oldRoot, logger, "Swap step 1 (target -> backup)");
            swapStarted = true;
            UpdaterInstallFileOps.MoveDirectoryWithRetry(stagingRoot, targetDir, logger, "Swap step 2 (staging -> target)");
            logger.Info("Atomic directory swap completed.");

            UpdaterInstallFileOps.WriteUpdateSecurityState(targetDir, plan.TrustedReleaseTag, logger);
            UpdaterInstallProcessOps.RestartApplication(targetDir, appExeRelativePath, logger);
            UpdaterInstallFileOps.SafeDeleteDirectory(oldRoot, logger);
            UpdaterInstallFileOps.SafeDeleteDirectory(preserveBackupRoot, logger);
            UpdaterInstallFileOps.SafeDeleteFile(trustedZipPath, logger);
            logger.Info("Atomic updater completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error($"Install failed: {ex.Message}");
            if (swapStarted && !Directory.Exists(targetDir) && Directory.Exists(oldRoot))
            {
                try
                {
                    UpdaterInstallFileOps.MoveDirectoryWithRetry(oldRoot, targetDir, logger, "Rollback step (backup -> target)", maxAttempts: 10, initialDelayMs: 250);
                    logger.Info("Rollback restored original target directory.");
                }
                catch (Exception rollbackEx)
                {
                    rollbackFailed = true;
                    logger.Error($"Rollback failed: {rollbackEx.Message}");
                    UpdaterInstallRecovery.WriteEmergencyRecoveryHint(targetDir, oldRoot, logger, plan.AppDisplayName);
                }
            }

            UpdaterInstallFileOps.SafeDeleteDirectory(stagingRoot, logger);
            UpdaterInstallFileOps.SafeDeleteDirectory(preserveBackupRoot, logger);
            UpdaterInstallFileOps.SafeDeleteFile(trustedZipPath, logger);
            if (rollbackFailed)
                logger.Error("Catastrophic rollback failure detected. Backup directory is intentionally preserved for manual recovery.");
            return 1;
        }
    }

    internal sealed class UpdateLockConflictException : InvalidOperationException
    {
        public UpdateLockConflictException(string message) : base(message)
        {
        }
    }
}
