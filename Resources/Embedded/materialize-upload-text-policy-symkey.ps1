# Writes Resources/Embedded/upload_text_policy.symkey from the environment (CI) or leaves an existing file untouched.
#
# GitHub Actions: set environment "release" secret UPLOAD_TEXT_POLICY_SYMKEY_B64 to the Base64 encoding
# of exactly 32 bytes (the raw AES-256 key file). Jobs must use `environment: release` so this secret is visible.
#
# Local: keep upload_text_policy.symkey on disk (gitignored); do not set the env var.
$ErrorActionPreference = 'Stop'

$repoRoot = if ($args.Count -ge 1 -and $args[0]) { [System.IO.Path]::GetFullPath($args[0]) } else { Get-Location }
$path = Join-Path $repoRoot 'Resources/Embedded/upload_text_policy.symkey'

$b64 = $env:UPLOAD_TEXT_POLICY_SYMKEY_B64
if (-not [string]::IsNullOrWhiteSpace($b64)) {
    $bytes = [Convert]::FromBase64String($b64.Trim())
    if ($bytes.Length -ne 32) {
        throw "UPLOAD_TEXT_POLICY_SYMKEY_B64 must decode to exactly 32 bytes; got $($bytes.Length)."
    }
    $dir = [System.IO.Path]::GetDirectoryName($path)
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [System.IO.File]::WriteAllBytes($path, $bytes)
    Write-Host "Wrote $path from UPLOAD_TEXT_POLICY_SYMKEY_B64 ($($bytes.Length) bytes)."
    exit 0
}

if (Test-Path -LiteralPath $path) {
    Write-Host "Using existing $path"
    exit 0
}

throw @"
Missing upload_text_policy.symkey and UPLOAD_TEXT_POLICY_SYMKEY_B64 is not set.

Local dev: create a 32-byte random key file at:
  $path

CI: add UPLOAD_TEXT_POLICY_SYMKEY_B64 on GitHub environment "release" (Base64 of those 32 bytes); workflow jobs use environment: release.
"@
