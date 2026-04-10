using System;
using System.Collections.Generic;

namespace Updater.Install;

internal sealed record InstallExecutionPlanDto(
    string ZipPackagePath,
    string TargetDirectoryPath,
    string AppExecutablePath,
    string AppDisplayName,
    IReadOnlyList<string> PreserveDirectoryNames,
    int ProcessIdToWaitFor,
    DateTimeOffset ProcessStartTimeUtc,
    string? TrustedReleaseTag,
    string ExpectedZipSha256,
    string InstallLogPath,
    bool RemoveOrphanFiles,
    DateTimeOffset CreatedAtUtc);
