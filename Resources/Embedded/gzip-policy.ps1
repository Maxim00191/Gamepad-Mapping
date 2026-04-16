# Regenerates upload_text_policy.txt.gz from upload_text_policy.txt (embedded in the app).
# Applies gzip, then XOR with upload_text_policy.xorkey (obfuscation for casual inspection only).
# Invoked by: .github/workflows/build.yml, publish/publish-release.ps1
#
# Plaintext is gitignored; CI has no upload_text_policy.txt. If the .txt is missing but .gz exists,
# we skip (build uses the committed obfuscated payload). Locally, keep .txt gitignored and run this
# script after editing the wordlist to refresh .gz before commit.
$ErrorActionPreference = 'Stop'
$p = Join-Path $PSScriptRoot 'upload_text_policy.txt'
$keyPath = Join-Path $PSScriptRoot 'upload_text_policy.xorkey'
$out = $p + '.gz'
if (-not (Test-Path -LiteralPath $p)) {
    if (Test-Path -LiteralPath $out) {
        Write-Host "Skip gzip-policy: plaintext not present ($p); using existing $out."
        exit 0
    }
    throw "Missing $p (and no $out to fall back on). Add upload_text_policy.txt locally or restore upload_text_policy.txt.gz."
}
if (-not (Test-Path -LiteralPath $keyPath)) {
    throw "Missing XOR key file: $keyPath"
}
$key = [System.IO.File]::ReadAllBytes($keyPath)
if ($key.Length -eq 0) {
    throw "XOR key file is empty: $keyPath"
}
$bytes = [System.IO.File]::ReadAllBytes($p)
$ms = New-Object System.IO.MemoryStream
try {
    $gz = New-Object System.IO.Compression.GZipStream($ms, [System.IO.Compression.CompressionLevel]::Optimal)
    try { $gz.Write($bytes, 0, $bytes.Length) }
    finally { $gz.Dispose() }
    $gzBytes = $ms.ToArray()
}
finally { $ms.Dispose() }
for ($i = 0; $i -lt $gzBytes.Length; $i++) {
    $gzBytes[$i] = $gzBytes[$i] -bxor $key[$i % $key.Length]
}
[System.IO.File]::WriteAllBytes($out, $gzBytes)
Write-Host "Wrote $out ($((Get-Item $out).Length) bytes)"
