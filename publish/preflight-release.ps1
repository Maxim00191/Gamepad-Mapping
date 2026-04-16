#Requires -Version 5.1
<#
.SYNOPSIS
    Release preflight checks for updater payload integrity.

.DESCRIPTION
    Runs fast, non-zip checks before production release:
    - validates updater payload manifest
    - performs an updater publish into a temp folder
    - verifies all required updater payload files are produced
    This script does not generate release zip artifacts.
#>
param(
    [string] $Configuration = "Release",
    [string] $RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$publishRoot = $PSScriptRoot
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path -Path $publishRoot -ChildPath ".."))
Set-Location -LiteralPath $repoRoot

$updaterProject = Join-Path $repoRoot "Updater\Updater.csproj"
$manifestPath = Join-Path $publishRoot "updater-required-files.txt"
$workflowPath = Join-Path $repoRoot ".github\workflows\build.yml"
$releaseScriptPath = Join-Path $publishRoot "publish-release.ps1"

foreach ($path in @($updaterProject, $manifestPath, $workflowPath, $releaseScriptPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required release file is missing: $path"
    }
}

$requiredUpdaterFiles = Get-Content -LiteralPath $manifestPath |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -and -not $_.StartsWith('#') }

if (-not $requiredUpdaterFiles -or $requiredUpdaterFiles.Count -eq 0) {
    throw "Updater payload manifest is empty: $manifestPath"
}

$duplicates = $requiredUpdaterFiles | Group-Object | Where-Object Count -gt 1
if ($duplicates) {
    $duplicateNames = ($duplicates | ForEach-Object Name) -join ", "
    throw "Updater payload manifest has duplicate entries: $duplicateNames"
}

foreach ($name in $requiredUpdaterFiles) {
    if ($name.IndexOfAny([char[]]@('\', '/')) -ge 0) {
        throw "Manifest entry must be a file name only (no path separators): $name"
    }
}

$mandatory = @('Updater.exe', 'Updater.dll', 'Updater.deps.json', 'Updater.runtimeconfig.json')
foreach ($required in $mandatory) {
    if ($requiredUpdaterFiles -notcontains $required) {
        throw "Updater payload manifest must include mandatory file: $required"
    }
}

$tempPublish = Join-Path ([System.IO.Path]::GetTempPath()) ("GamepadMapping-Updater-Preflight-" + [Guid]::NewGuid().ToString("N"))
try {
    New-Item -ItemType Directory -Path $tempPublish | Out-Null

    Write-Host "Publishing updater for preflight validation..." -ForegroundColor Cyan
    & dotnet publish $updaterProject -c $Configuration -r $RuntimeIdentifier --self-contained false `
        -o $tempPublish `
        -p:DebugType=none `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for updater preflight validation."
    }

    foreach ($required in $requiredUpdaterFiles) {
        $path = Join-Path $tempPublish $required
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Updater preflight failed. Missing published payload file: $path"
        }
    }

    Write-Host "Preflight passed. Updater payload manifest and publish output are consistent." -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $tempPublish) {
        Remove-Item -LiteralPath $tempPublish -Recurse -Force
    }
}
