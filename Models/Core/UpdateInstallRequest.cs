using System.Collections.Generic;

namespace GamepadMapperGUI.Models.Core;

public sealed record UpdateInstallRequest(
    string ZipPackagePath,
    string TargetDirectoryPath,
    string AppExecutablePath,
    IReadOnlyList<string> PreserveDirectoryNames,
    int ProcessIdToWaitFor,
    string? TrustedReleaseTag = null,
    string? ExpectedZipSha256 = null,
    string? InstallLogPath = null,
    bool RemoveOrphanFiles = true);
