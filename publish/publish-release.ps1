#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the same win-x64 publish outputs and zips as .github/workflows/build.yml (release job).
    Writes build outputs under this folder (publish/single, publish/fx, publish/*.zip).

.PARAMETER Tag
    Used in zip file names (e.g. v1.2.0). If omitted, uses the exact git tag at HEAD, or "local".

.EXAMPLE
    .\publish\publish-release.ps1
.EXAMPLE
    .\publish\publish-release.ps1 -Tag v1.2.0
#>
param(
    [string] $Tag = ""
)

$ErrorActionPreference = "Stop"

$publishRoot = $PSScriptRoot
$repoRoot = Split-Path -LiteralPath $publishRoot -Parent
Set-Location -LiteralPath $repoRoot

$csproj = Join-Path $repoRoot "Gamepad Mapping.csproj"
if (-not (Test-Path -LiteralPath $csproj)) {
    throw "Project not found: $csproj (keep this script in the publish/ folder at the repo root)."
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
    Push-Location -LiteralPath $repoRoot
    try {
        $described = (& git describe --tags --exact-match 2>$null)
        if ($LASTEXITCODE -eq 0 -and $described) {
            $Tag = $described.Trim()
        }
        else {
            $Tag = $null
        }
    }
    finally {
        Pop-Location
    }
    if ([string]::IsNullOrWhiteSpace($Tag)) {
        $Tag = "local"
    }
}

$publishSingle = Join-Path $publishRoot "single"
$publishFx = Join-Path $publishRoot "fx"

foreach ($dir in @($publishSingle, $publishFx)) {
    if (Test-Path -LiteralPath $dir) {
        Remove-Item -LiteralPath $dir -Recurse -Force
    }
}

Write-Host "dotnet publish: single-file, self-contained win-x64 -> publish\single" -ForegroundColor Cyan
& dotnet publish $csproj -c Release -r win-x64 --self-contained true `
    -o $publishSingle `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "dotnet publish: framework-dependent win-x64 -> publish\fx" -ForegroundColor Cyan
& dotnet publish $csproj -c Release -r win-x64 --self-contained false `
    -o $publishFx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$zipSingle = Join-Path $publishRoot "Gamepad-Mapping-$Tag-win-x64-single.zip"
$zipFx = Join-Path $publishRoot "Gamepad-Mapping-$Tag-win-x64-fx.zip"

foreach ($zip in @($zipSingle, $zipFx)) {
    if (Test-Path -LiteralPath $zip) {
        Remove-Item -LiteralPath $zip -Force
    }
}

Write-Host "Zipping..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishSingle "*") -DestinationPath $zipSingle
Compress-Archive -Path (Join-Path $publishFx "*") -DestinationPath $zipFx

Write-Host "Done:" -ForegroundColor Green
Write-Host "  $zipSingle"
Write-Host "  $zipFx"
