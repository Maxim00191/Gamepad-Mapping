using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services;

public sealed class UpdateInstallerService : IUpdateInstallerService
{
    public bool TryLaunchInstaller(UpdateInstallRequest request, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            if (request is null)
            {
                errorMessage = "Installer request is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ZipPackagePath) || !File.Exists(request.ZipPackagePath))
            {
                errorMessage = "Update package ZIP file does not exist.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.TargetDirectoryPath) || !Directory.Exists(request.TargetDirectoryPath))
            {
                errorMessage = "Target install directory does not exist.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.AppExecutablePath))
            {
                errorMessage = "App executable path is required.";
                return false;
            }

            var needsElevation = !CanWriteToDirectory(request.TargetDirectoryPath);

            if (!VerifyPackageHashIfProvided(request.ZipPackagePath, request.ExpectedZipSha256, out var hashError))
            {
                errorMessage = hashError;
                return false;
            }

            var preserveNames = (request.PreserveDirectoryNames ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().Trim('\\', '/'))
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var scriptPath = EnsureInstallerScript();
            var preserveCsv = string.Join(",", preserveNames);
            var installLogPath = ResolveInstallLogPath(request.InstallLogPath);
            var expectedSha256 = request.ExpectedZipSha256?.Trim().ToLowerInvariant() ?? string.Empty;
            var zipRelativePath = Path.GetRelativePath(request.TargetDirectoryPath, request.ZipPackagePath);
            var appExeRelativePath = Path.GetRelativePath(request.TargetDirectoryPath, request.AppExecutablePath);
            var logRelativePath = Path.GetRelativePath(request.TargetDirectoryPath, installLogPath);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = true,
                Verb = needsElevation ? "runas" : "open",
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? request.TargetDirectoryPath
            };

            // Use -Command with a script block for more robust quoting and error handling
            // We use single quotes for internal strings and double-double quotes for the whole command
            string Escape(string? val) => (val ?? "").Replace("'", "''");

            var scriptBlock = 
                $"& {{ " +
                $"$ErrorActionPreference = 'Stop'; " +
                $". '{Escape(scriptPath)}' " +
                $"-TargetDir '{Escape(request.TargetDirectoryPath)}' " +
                $"-ZipRelativePath '{Escape(zipRelativePath)}' " +
                $"-AppExeRelativePath '{Escape(appExeRelativePath)}' " +
                $"-LogRelativePath '{Escape(logRelativePath)}' " +
                $"-PreserveDirsCsv '{Escape(preserveCsv)}' " +
                $"-WaitForProcessId {request.ProcessIdToWaitFor} " +
                $"-ExpectedSha256 '{Escape(expectedSha256)}' " +
                $"-RemoveOrphanFiles:${(request.RemoveOrphanFiles ? "true" : "false")}; " +
                $"if ($LASTEXITCODE -ne 0) {{ Write-Host 'Installation failed with exit code ' $LASTEXITCODE; Read-Host 'Press Enter to exit...' }} " +
                $"}}";

            psi.Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{scriptBlock.Replace("\"", "\"\"")}\"";

            var process = Process.Start(psi);
            if (process is null)
            {
                errorMessage = "Failed to start installer script process.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to launch installer script: {ex.Message}";
            return false;
        }
    }

    private static string EnsureInstallerScript()
    {
        var updatesDir = AppPaths.GetUpdateDownloadsDirectory();
        var installerDir = Path.Combine(updatesDir, "installer");
        Directory.CreateDirectory(installerDir);

        var scriptPath = Path.Combine(installerDir, "install-update.ps1");
        File.WriteAllText(scriptPath, BuildScriptContent(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return scriptPath;
    }

    private static string ResolveInstallLogPath(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
            return requestedPath;

        var logsDir = AppPaths.GetLogsDirectory();
        var fileName = $"update-install-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log";
        return Path.Combine(logsDir, fileName);
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

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static bool VerifyPackageHashIfProvided(string zipPath, string? expectedSha256, out string? errorMessage)
    {
        errorMessage = null;
        var expected = expectedSha256?.Trim();
        if (string.IsNullOrWhiteSpace(expected))
            return true;

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

    private static string BuildScriptContent() =>
        """
param(
    [Parameter(Mandatory = $true)][string]$TargetDir,
    [Parameter(Mandatory = $true)][string]$ZipRelativePath,
    [Parameter(Mandatory = $true)][string]$AppExeRelativePath,
    [Parameter(Mandatory = $true)][string]$LogRelativePath,
    [string]$PreserveDirsCsv = "",
    [int]$WaitForProcessId = 0,
    [string]$ExpectedSha256 = "",
    [bool]$RemoveOrphanFiles = $true
)

$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $line = "[$timestamp] $Message"
    try {
        Add-Content -LiteralPath $script:EffectiveLogPath -Value $line -ErrorAction Stop
    } catch {
        try {
            $fallback = Join-Path ([System.IO.Path]::GetTempPath()) ("GamepadMapping-update-install-fallback-" + (Get-Date -Format "yyyyMMdd") + ".log")
            Add-Content -LiteralPath $fallback -Value $line -ErrorAction SilentlyContinue
        } catch {
            # Ignore final fallback failure.
        }
    }
    Write-Host $line
}

function Normalize-RelativePath {
    param([string]$Path)
    return ($Path -replace "/", "\")
}

function Is-PreservedRelativePath {
    param(
        [string]$RelativePath,
        [string[]]$PreserveDirs
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $false
    }

    $normalized = Normalize-RelativePath $RelativePath
    foreach ($dir in $PreserveDirs) {
        if ([string]::IsNullOrWhiteSpace($dir)) {
            continue
        }

        $prefix = (Normalize-RelativePath $dir).Trim("\")
        if ($normalized.Equals($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }

        if ($normalized.StartsWith("$prefix\", [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Is-ReservedSystemRelativePath {
    param([string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $true
    }
    $normalized = Normalize-RelativePath $RelativePath
    $reservedPrefixes = @(
        ".git",
        ".vs",
        "Logs",
        "Updates"
    )

    foreach ($prefix in $reservedPrefixes) {
        if ($normalized.Equals($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
        if ($normalized.StartsWith("$prefix\", [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Is-MergeTargetRelativePath {
    param([string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $false
    }

    $normalized = Normalize-RelativePath $RelativePath
    return $normalized.Equals("Assets\Config\default.json", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.Equals("Assets\Config\default_settings.json", [System.StringComparison]::OrdinalIgnoreCase)
}

function Merge-JsonObject {
    param(
        [hashtable]$BaseObject,
        [hashtable]$OverlayObject
    )

    $result = @{}
    if ($BaseObject -ne $null) {
        foreach ($key in $BaseObject.Keys) {
            $result[$key] = $BaseObject[$key]
        }
    }

    if ($OverlayObject -eq $null) {
        return $result
    }

    foreach ($key in $OverlayObject.Keys) {
        $baseValue = $null
        $hasBase = $result.ContainsKey($key)
        if ($hasBase) {
            $baseValue = $result[$key]
        }
        $overlayValue = $OverlayObject[$key]

        if ($hasBase -and $baseValue -is [hashtable] -and $overlayValue -is [hashtable]) {
            $result[$key] = Merge-JsonObject -BaseObject $baseValue -OverlayObject $overlayValue
            continue
        }

        # Local value wins on conflict; package only provides missing/new shape.
        $result[$key] = $overlayValue
    }

    return $result
}

function ConvertTo-FormattedJsonObject {
    param(
        [string]$JsonRaw
    )

    $jsonObject = ConvertFrom-Json -InputObject $JsonRaw -AsHashtable -Depth 100
    $formattedJson = $jsonObject | ConvertTo-Json -Depth 100
    $normalizedObject = ConvertFrom-Json -InputObject $formattedJson -AsHashtable -Depth 100
    return @{
        Object = $normalizedObject
        FormattedJson = $formattedJson
    }
}

function Wait-FileUnlock {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$MaxAttempts = 20,
        [int]$DelayMilliseconds = 250
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
            $stream.Close()
            return
        } catch {
            if ($attempt -eq $MaxAttempts) {
                throw
            }
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }
}

function Stop-AppProcessesByPath {
    param(
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [string]$ExcludeProcessId = ""
    )

    $targetFullPath = [System.IO.Path]::GetFullPath($ExecutablePath)
    $targetName = [System.IO.Path]::GetFileNameWithoutExtension($targetFullPath)
    $matchingProcesses = @(Get-Process -Name $targetName -ErrorAction SilentlyContinue)

    foreach ($proc in $matchingProcesses) {
        if (-not [string]::IsNullOrWhiteSpace($ExcludeProcessId) -and $proc.Id.ToString() -eq $ExcludeProcessId) {
            continue
        }

        $procPath = $null
        try {
            $procPath = $proc.MainModule.FileName
        } catch {
            # Access denied for some processes; skip path filtering and try by name.
        }

        if ($procPath -ne $null) {
            $procPath = [System.IO.Path]::GetFullPath($procPath)
            if (-not $procPath.Equals($targetFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }
        }

        try {
            Write-Log "Stopping process $($proc.ProcessName) (PID=$($proc.Id)) before file replacement."
            Stop-Process -Id $proc.Id -Force -ErrorAction Stop
        } catch {
            Write-Log "Failed to stop process PID=$($proc.Id): $($_.Exception.Message)"
        }
    }
}

$zipPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($TargetDir, $ZipRelativePath))
$appExePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($TargetDir, $AppExeRelativePath))
$script:EffectiveLogPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($TargetDir, $LogRelativePath))
try {
    $logDir = Split-Path -Path $script:EffectiveLogPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($logDir) -and -not (Test-Path -LiteralPath $logDir)) {
        New-Item -Path $logDir -ItemType Directory -Force | Out-Null
    }
} catch {
    $script:EffectiveLogPath = Join-Path ([System.IO.Path]::GetTempPath()) ("GamepadMapping-update-install-fallback-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")
}
Write-Log "Installer bootstrap. EffectiveLogPath=$script:EffectiveLogPath"

if ($WaitForProcessId -gt 0) {
    try {
        Wait-Process -Id $WaitForProcessId -ErrorAction SilentlyContinue
    } catch {
        # Ignore.
    }
}

Stop-AppProcessesByPath -ExecutablePath $appExePath -ExcludeProcessId "$WaitForProcessId"

if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    throw "ZIP package not found: $zipPath"
}

if (-not (Test-Path -LiteralPath $TargetDir -PathType Container)) {
    throw "Target directory not found: $TargetDir"
}

if ([string]::IsNullOrWhiteSpace($appExePath)) {
    throw "App executable path is required."
}

Write-Log "Installer started. Zip=$zipPath Target=$TargetDir"

if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256)) {
    $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not $actualHash.Equals($ExpectedSha256.ToLowerInvariant(), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "SHA-256 mismatch. Expected=$ExpectedSha256 Actual=$actualHash"
    }
    Write-Log "SHA-256 verification passed."
}

$preserveDirs = @()
if (-not [string]::IsNullOrWhiteSpace($PreserveDirsCsv)) {
    $preserveDirs = $PreserveDirsCsv.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object {
        $_.Trim().Trim("\", "/")
    } | Where-Object { $_ -ne "" }
}

$extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("GamepadMapping_Update_" + [Guid]::NewGuid().ToString("N"))
New-Item -Path $extractRoot -ItemType Directory -Force | Out-Null
$backupRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("GamepadMapping_Backup_" + [Guid]::NewGuid().ToString("N"))
New-Item -Path $backupRoot -ItemType Directory -Force | Out-Null
$createdFiles = [System.Collections.Generic.List[string]]::new()
$backedUpFiles = [System.Collections.Generic.List[string]]::new()
$removedFiles = [System.Collections.Generic.List[string]]::new()

try {
    Expand-Archive -Path $zipPath -DestinationPath $extractRoot -Force
    Write-Log "Package extracted: $extractRoot"

    $rootItems = Get-ChildItem -LiteralPath $extractRoot
    $sourceRoot = $extractRoot
    if ($rootItems.Count -eq 1 -and $rootItems[0].PSIsContainer) {
        $sourceRoot = $rootItems[0].FullName
    }

    $files = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File
    $packageFileSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($sourceRoot.Length).TrimStart("\", "/")
        if ([string]::IsNullOrWhiteSpace($relative)) {
            continue
        }
        $normalizedRelative = Normalize-RelativePath $relative
        $packageFileSet.Add($normalizedRelative) | Out-Null
    }

    if ($RemoveOrphanFiles) {
        $targetFiles = Get-ChildItem -LiteralPath $TargetDir -Recurse -File
        foreach ($targetFile in $targetFiles) {
            $targetRelative = $targetFile.FullName.Substring($TargetDir.Length).TrimStart("\", "/")
            if ([string]::IsNullOrWhiteSpace($targetRelative)) {
                continue
            }

            $normalizedTargetRelative = Normalize-RelativePath $targetRelative
            if (Is-PreservedRelativePath -RelativePath $normalizedTargetRelative -PreserveDirs $preserveDirs) {
                continue
            }

            if (Is-ReservedSystemRelativePath -RelativePath $normalizedTargetRelative) {
                continue
            }

            if ($packageFileSet.Contains($normalizedTargetRelative)) {
                continue
            }

            $backupPath = Join-Path $backupRoot $normalizedTargetRelative
            $backupDir = Split-Path -Path $backupPath -Parent
            if (-not [string]::IsNullOrWhiteSpace($backupDir)) {
                New-Item -Path $backupDir -ItemType Directory -Force | Out-Null
            }
            Copy-Item -LiteralPath $targetFile.FullName -Destination $backupPath -Force
            Remove-Item -LiteralPath $targetFile.FullName -Force
            $removedFiles.Add($normalizedTargetRelative) | Out-Null
        }
        Write-Log "Orphan cleanup completed. Removed=$($removedFiles.Count)"
    }

    foreach ($file in $files) {
        $relative = $file.FullName.Substring($sourceRoot.Length).TrimStart("\", "/")
        if ([string]::IsNullOrWhiteSpace($relative)) {
            continue
        }

        $normalizedRelative = Normalize-RelativePath $relative
        if (Is-PreservedRelativePath -RelativePath $normalizedRelative -PreserveDirs $preserveDirs) {
            continue
        }

        $destinationPath = Join-Path $TargetDir $normalizedRelative
        $destinationDir = Split-Path -Path $destinationPath -Parent
        if (-not [string]::IsNullOrWhiteSpace($destinationDir)) {
            New-Item -Path $destinationDir -ItemType Directory -Force | Out-Null
        }

        if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
            $backupPath = Join-Path $backupRoot $normalizedRelative
            $backupDir = Split-Path -Path $backupPath -Parent
            if (-not [string]::IsNullOrWhiteSpace($backupDir)) {
                New-Item -Path $backupDir -ItemType Directory -Force | Out-Null
            }
            Copy-Item -LiteralPath $destinationPath -Destination $backupPath -Force
            $backedUpFiles.Add($normalizedRelative) | Out-Null
        } else {
            $createdFiles.Add($normalizedRelative) | Out-Null
        }

        if ((Test-Path -LiteralPath $destinationPath -PathType Leaf) -and (Is-MergeTargetRelativePath -RelativePath $normalizedRelative)) {
            try {
                Wait-FileUnlock -Path $destinationPath
                $packageJsonRaw = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
                $localJsonRaw = Get-Content -LiteralPath $destinationPath -Raw -Encoding UTF8
                $packageNormalized = ConvertTo-FormattedJsonObject -JsonRaw $packageJsonRaw
                $localNormalized = ConvertTo-FormattedJsonObject -JsonRaw $localJsonRaw
                $packageJson = $packageNormalized.Object
                $localJson = $localNormalized.Object
                $mergedJson = Merge-JsonObject -BaseObject $packageJson -OverlayObject $localJson
                $mergedJsonText = $mergedJson | ConvertTo-Json -Depth 100
                [System.IO.File]::WriteAllText($destinationPath, $mergedJsonText, [System.Text.UTF8Encoding]::new($false))
                Write-Log "Merged config file: $normalizedRelative"
                continue
            } catch {
                Write-Log "Config merge failed, fallback to overwrite: $normalizedRelative Error=$($_.Exception.Message)"
            }
        }

        if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
            Wait-FileUnlock -Path $destinationPath
        }
        Copy-Item -LiteralPath $file.FullName -Destination $destinationPath -Force
    }
    Write-Log "Install completed successfully."

    if (Test-Path -LiteralPath $appExePath -PathType Leaf) {
        Start-Process -FilePath $appExePath | Out-Null
        Write-Log "Application restarted: $appExePath"
    } else {
        Write-Log "Skip restart because executable not found: $appExePath"
    }
}
catch {
    Write-Log "Install failed. Starting rollback. Error=$($_.Exception.Message)"
    foreach ($relative in $backedUpFiles) {
        $sourceBackup = Join-Path $backupRoot $relative
        $destPath = Join-Path $TargetDir $relative
        $destDir = Split-Path -Path $destPath -Parent
        if (-not [string]::IsNullOrWhiteSpace($destDir)) {
            New-Item -Path $destDir -ItemType Directory -Force | Out-Null
        }
        Copy-Item -LiteralPath $sourceBackup -Destination $destPath -Force
    }
    foreach ($relative in $createdFiles) {
        $destPath = Join-Path $TargetDir $relative
        if (Test-Path -LiteralPath $destPath -PathType Leaf) {
            Remove-Item -LiteralPath $destPath -Force -ErrorAction SilentlyContinue
        }
    }
    foreach ($relative in $removedFiles) {
        $sourceBackup = Join-Path $backupRoot $relative
        $destPath = Join-Path $TargetDir $relative
        $destDir = Split-Path -Path $destPath -Parent
        if (-not [string]::IsNullOrWhiteSpace($destDir)) {
            New-Item -Path $destDir -ItemType Directory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $sourceBackup -PathType Leaf) {
            Copy-Item -LiteralPath $sourceBackup -Destination $destPath -Force
        }
    }
    Write-Log "Rollback finished."
    throw
}
finally {
    if (Test-Path -LiteralPath $extractRoot) {
        Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $backupRoot) {
        Remove-Item -LiteralPath $backupRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
""";
}
