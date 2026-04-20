#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the same win-x64 publish outputs and zips as .github/workflows/build.yml (release job).
    Refreshes Resources/Embedded/upload_text_policy.payload (gzip + AES-256-GCM) via gzip-policy.ps1 before publish
    (same PowerShell host as CI when started via publish-release.bat, which prefers pwsh).
    Requires Resources/Embedded/upload_text_policy.symkey (32 bytes, gitignored). Optional: set env
    UPLOAD_TEXT_POLICY_SYMKEY_B64 (same Base64 as GitHub environment "release" secret) to materialize the file.
    Writes build outputs under this folder (publish/single, publish/fx, publish/*.zip).

.PARAMETER Tag
    Used in zip file names (e.g. v1.4.0). If omitted, uses the exact git tag at HEAD, or "local".

.EXAMPLE
    .\publish\publish-release.ps1
.EXAMPLE
    .\publish\publish-release.ps1 -Tag v1.4.0
#>
param(
    [string] $Tag = ""
)

$ErrorActionPreference = "Stop"

$publishRoot = $PSScriptRoot
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path -Path $publishRoot -ChildPath ".."))
Set-Location -LiteralPath $repoRoot

$csproj = Join-Path $repoRoot "Gamepad Mapping.csproj"
if (-not (Test-Path -LiteralPath $csproj)) {
    throw "Project not found: $csproj (keep this script in the publish/ folder at the repo root)."
}

$materializeSymKey = Join-Path $repoRoot "Resources\Embedded\materialize-upload-text-policy-symkey.ps1"
if (Test-Path -LiteralPath $materializeSymKey) {
    & $materializeSymKey $repoRoot
}

$gzipPolicyScript = Join-Path $repoRoot "Resources\Embedded\gzip-policy.ps1"
if (-not (Test-Path -LiteralPath $gzipPolicyScript)) {
    throw "gzip-policy.ps1 not found: $gzipPolicyScript"
}
Write-Host "Refreshing embedded upload text policy (gzip)..." -ForegroundColor Cyan
& $gzipPolicyScript

$validatePayloadScript = Join-Path $repoRoot "Resources\Embedded\validate-upload-text-policy-payload.ps1"
if (-not (Test-Path -LiteralPath $validatePayloadScript)) {
    throw "Payload validator script not found: $validatePayloadScript"
}
Write-Host "Validating embedded upload text policy envelope..." -ForegroundColor Cyan
& $validatePayloadScript

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
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:ObfuscateUploadTextPolicyPayload=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "dotnet publish: framework-dependent win-x64 -> publish\fx" -ForegroundColor Cyan
& dotnet publish $csproj -c Release -r win-x64 --self-contained false `
    -o $publishFx `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:ObfuscateUploadTextPolicyPayload=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "dotnet publish: updater payload (win-x64, framework-dependent) -> publish\\updater" -ForegroundColor Cyan
& dotnet publish (Join-Path $repoRoot "Updater\Updater.csproj") -c Release -r win-x64 --self-contained false `
    -o $publishUpdater `
    -p:DebugType=none `
    -p:DebugSymbols=false
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
