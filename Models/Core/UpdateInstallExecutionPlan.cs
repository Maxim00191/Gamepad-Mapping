using System;
using System.Collections.Generic;

namespace GamepadMapperGUI.Models.Core;

public sealed record UpdateInstallExecutionPlan(
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
