using System;
using System.Collections.Generic;

namespace Updater.Install;

internal sealed record InstallExecutionPlanDto(
    string ZipPackagePath,
    string TargetDirectoryPath,
    string AppExecutablePath,
    IReadOnlyList<string> PreserveDirectoryNames,
    int ProcessIdToWaitFor,
    string? TrustedReleaseTag,
    string ExpectedZipSha256,
    string InstallLogPath,
    bool RemoveOrphanFiles,
    DateTimeOffset CreatedAtUtc);
