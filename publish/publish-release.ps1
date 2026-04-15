#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the same win-x64 publish outputs and zips as .github/workflows/build.yml (release job).
    Writes build outputs under this folder (publish/single, publish/fx, publish/*.zip).

.PARAMETER Tag
    Used in zip file names (e.g. v1.4.0). If omitted, uses the exact git tag at HEAD, or "local".

.PARAMETER CommunityProfilesUploadWorkerApiKey
    Optional local override for COMMUNITY_PROFILES_UPLOAD_WORKER_API_KEY.

.PARAMETER CommunityProfilesUploadWorkerSigningKey
    Optional local override for COMMUNITY_PROFILES_UPLOAD_WORKER_SIGNING_KEY.

.PARAMETER UseHardcodedWorkerSecrets
    If set, uses hardcoded secrets in this script when CLI args and environment variables are not provided.

.PARAMETER LocalSecretsPath
    Optional path to a local secrets script that sets
    COMMUNITY_PROFILES_UPLOAD_WORKER_API_KEY and COMMUNITY_PROFILES_UPLOAD_WORKER_SIGNING_KEY.

.EXAMPLE
    .\publish\publish-release.ps1
.EXAMPLE
    .\publish\publish-release.ps1 -Tag v1.4.0
.EXAMPLE
    $env:COMMUNITY_PROFILES_UPLOAD_WORKER_API_KEY = "..."
    $env:COMMUNITY_PROFILES_UPLOAD_WORKER_SIGNING_KEY = "..."
    .\publish\publish-release.ps1 -Tag v1.4.0
.EXAMPLE
    .\publish\publish-release.ps1 -Tag v1.4.0 -UseHardcodedWorkerSecrets
.EXAMPLE
    .\publish\publish-release.ps1 -Tag v1.4.0 -LocalSecretsPath .\publish\local-secrets.ps1
#>
param(
    [string] $Tag = "",
    [string] $CommunityProfilesUploadWorkerApiKey = "",
    [string] $CommunityProfilesUploadWorkerSigningKey = "",
    [switch] $UseHardcodedWorkerSecrets,
    [string] $LocalSecretsPath = ""
)

$ErrorActionPreference = "Stop"

function Convert-ToBase64Utf8([string] $value) {
    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($value))
}

$publishRoot = $PSScriptRoot
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path -Path $publishRoot -ChildPath ".."))
Set-Location -LiteralPath $repoRoot

$resolvedLocalSecretsPath = if (-not [string]::IsNullOrWhiteSpace($LocalSecretsPath)) {
    if ([System.IO.Path]::IsPathRooted($LocalSecretsPath)) {
        $LocalSecretsPath
    }
    else {
        Join-Path $repoRoot $LocalSecretsPath
    }
}
else {
    Join-Path $publishRoot "local-secrets.ps1"
}

if (Test-Path -LiteralPath $resolvedLocalSecretsPath) {
    . $resolvedLocalSecretsPath
}

$csproj = Join-Path $repoRoot "Gamepad Mapping.csproj"
if (-not (Test-Path -LiteralPath $csproj)) {
    throw "Project not found: $csproj (keep this script in the publish/ folder at the repo root)."
}

$updaterManifestPath = Join-Path $publishRoot "updater-required-files.txt"
if (-not (Test-Path -LiteralPath $updaterManifestPath)) {
    throw "Updater payload manifest not found: $updaterManifestPath"
}

$requiredUpdaterFiles = Get-Content -LiteralPath $updaterManifestPath |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -and -not $_.StartsWith('#') }

if (-not $requiredUpdaterFiles -or $requiredUpdaterFiles.Count -eq 0) {
    throw "Updater payload manifest is empty: $updaterManifestPath"
}

# Optional local-only fallback. Fill these if you want true hardcoded local release secrets.
$hardcodedCommunityProfilesUploadWorkerApiKey = ""
$hardcodedCommunityProfilesUploadWorkerSigningKey = ""

$resolvedUploadWorkerApiKey = if (-not [string]::IsNullOrWhiteSpace($CommunityProfilesUploadWorkerApiKey)) {
    $CommunityProfilesUploadWorkerApiKey
}
elseif (-not [string]::IsNullOrWhiteSpace($script:COMMUNITY_PROFILES_UPLOAD_WORKER_API_KEY)) {
    $script:COMMUNITY_PROFILES_UPLOAD_WORKER_API_KEY
}
elseif (-not [string]::IsNullOrWhiteSpace($env:COMMUNITY_PROFILES_UPLOAD_WORKER_API_KEY)) {
    $env:COMMUNITY_PROFILES_UPLOAD_WORKER_API_KEY
}
elseif ($UseHardcodedWorkerSecrets.IsPresent) {
    $hardcodedCommunityProfilesUploadWorkerApiKey
}
else {
    ""
}

$resolvedUploadWorkerSigningKey = if (-not [string]::IsNullOrWhiteSpace($CommunityProfilesUploadWorkerSigningKey)) {
    $CommunityProfilesUploadWorkerSigningKey
}
elseif (-not [string]::IsNullOrWhiteSpace($script:COMMUNITY_PROFILES_UPLOAD_WORKER_SIGNING_KEY)) {
    $script:COMMUNITY_PROFILES_UPLOAD_WORKER_SIGNING_KEY
}
elseif (-not [string]::IsNullOrWhiteSpace($env:COMMUNITY_PROFILES_UPLOAD_WORKER_SIGNING_KEY)) {
    $env:COMMUNITY_PROFILES_UPLOAD_WORKER_SIGNING_KEY
}
elseif ($UseHardcodedWorkerSecrets.IsPresent) {
    $hardcodedCommunityProfilesUploadWorkerSigningKey
}
else {
    ""
}

if ([string]::IsNullOrWhiteSpace($resolvedUploadWorkerApiKey)) {
    throw "Missing COMMUNITY_PROFILES_UPLOAD_WORKER_API_KEY. Provide -CommunityProfilesUploadWorkerApiKey, set env var, or use -UseHardcodedWorkerSecrets with a value in this script."
}
if ([string]::IsNullOrWhiteSpace($resolvedUploadWorkerSigningKey)) {
    throw "Missing COMMUNITY_PROFILES_UPLOAD_WORKER_SIGNING_KEY. Provide -CommunityProfilesUploadWorkerSigningKey, set env var, or use -UseHardcodedWorkerSecrets with a value in this script."
}

$uploadWorkerApiKeyB64 = Convert-ToBase64Utf8 $resolvedUploadWorkerApiKey
$uploadWorkerSigningKeyB64 = Convert-ToBase64Utf8 $resolvedUploadWorkerSigningKey

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
$publishUpdater = Join-Path $publishRoot "updater"

foreach ($dir in @($publishSingle, $publishFx, $publishUpdater)) {
    if (Test-Path -LiteralPath $dir) {
        Remove-Item -LiteralPath $dir -Recurse -Force
    }
}

$localSettingsFile = Join-Path $repoRoot "Assets\Config\local_settings.json"
$tempLocalSettingsFile = Join-Path $publishRoot "local_settings.json.tmp"
if (Test-Path -LiteralPath $localSettingsFile) {
    Write-Host "Temporarily moving local_settings.json..." -ForegroundColor Yellow
    Move-Item -LiteralPath $localSettingsFile -Destination $tempLocalSettingsFile -Force
}

Write-Host "dotnet publish: single-file, self-contained win-x64 -> publish\single" -ForegroundColor Cyan
& dotnet publish $csproj -c Release -r win-x64 --self-contained true `
    -o $publishSingle `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    "-p:CommunityProfilesUploadWorkerApiKeyBase64=$uploadWorkerApiKeyB64" `
    "-p:CommunityProfilesUploadWorkerSigningKeyBase64=$uploadWorkerSigningKeyB64"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "dotnet publish: framework-dependent win-x64 -> publish\fx" -ForegroundColor Cyan
& dotnet publish $csproj -c Release -r win-x64 --self-contained false `
    -o $publishFx `
    "-p:CommunityProfilesUploadWorkerApiKeyBase64=$uploadWorkerApiKeyB64" `
    "-p:CommunityProfilesUploadWorkerSigningKeyBase64=$uploadWorkerSigningKeyB64"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "dotnet publish: updater payload (win-x64, framework-dependent) -> publish\\updater" -ForegroundColor Cyan
& dotnet publish (Join-Path $repoRoot "Updater\Updater.csproj") -c Release -r win-x64 --self-contained false `
    -o $publishUpdater
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$docFiles = @('README.md', 'README_zh.md', 'CHANGELOG.md', 'RELEASE_NOTES.md')
foreach ($outDir in @($publishSingle, $publishFx)) {
    foreach ($rel in $docFiles) {
        $src = Join-Path $repoRoot $rel
        if (Test-Path -LiteralPath $src) {
            Copy-Item -LiteralPath $src -Destination $outDir -Force
        }
    }
}

foreach ($f in $requiredUpdaterFiles) {
    $src = Join-Path $publishUpdater $f
    if (-not (Test-Path -LiteralPath $src)) {
        throw "Updater publish output missing required file: $src"
    }
    Copy-Item -LiteralPath $src -Destination $publishSingle -Force
    Copy-Item -LiteralPath $src -Destination $publishFx -Force
}

# --- Digital Signature (Local) ---
# Sign executables using a certificate from the Windows Certificate Store.
$certThumbprint = "B24744482F4EA296BB1CBD1DE4E7CCAF0607199A"

# Try to find signtool.exe in common Windows SDK locations, or use 'where' as fallback
$signtool = $null
$sdkPaths = @(
    "D:\Windows Kits\10\bin",
    "C:\Program Files\Windows Kits\10\bin",
    "C:\Program Files (x86)\Windows Kits\10\bin",
    "C:\Program Files (x86)\Windows Kits\8.1\bin",
    "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin"
)

foreach ($path in $sdkPaths) {
    if (Test-Path $path) {
        # Search for signtool.exe in any subfolder (to handle versioned folders like 10.0.22621.0)
        $found = Get-ChildItem -Path $path -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue | 
                 Where-Object { $_.FullName -match "x64" } | 
                 Sort-Object LastWriteTime -Descending | 
                 Select-Object -First 1 -ExpandProperty FullName
        if ($found) { $signtool = $found; break }
    }
}

if (-not $signtool) {
    # Fallback to system PATH
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { $signtool = $cmd.Source }
}

if ($signtool) {
    Write-Host "Using signtool: $signtool" -ForegroundColor Gray
    Write-Host "Signing executables with thumbprint: $certThumbprint" -ForegroundColor Cyan
    $filesToSign = Get-ChildItem -Path $publishSingle, $publishFx -Include "*.exe", "*.dll" -Recurse | Select-Object -ExpandProperty FullName
    foreach ($f in $filesToSign) {
        Write-Host "  Signing: $f"
        # /s My: Use "Personal" store in Current User context (default)
        # /sha1: specify thumbprint
        # /tr and /td sha256: RFC 3161 timestamping
        & $signtool sign /s My /sha1 $certThumbprint /tr http://timestamp.digicert.com /td sha256 /fd sha256 /v "$f"
    }
}
else {
    Write-Warning "signtool.exe not found. Skipping local signing."
}

foreach ($outDir in @($publishSingle, $publishFx)) {
    foreach ($f in $requiredUpdaterFiles) {
        $p = Join-Path $outDir $f
        if (-not (Test-Path -LiteralPath $p)) {
            throw "Publish output missing required updater file: $p"
        }
    }
}

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
