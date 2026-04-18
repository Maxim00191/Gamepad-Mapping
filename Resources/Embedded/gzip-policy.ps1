# Regenerates upload_text_policy.payload from upload_text_policy.txt (embedded in GamepadMapperGUI.UploadTextPolicyPayload).
# Applies gzip, then AES-256-GCM authenticated encryption using upload_text_policy.symkey (32 bytes).
# Invoked by: .github/workflows/build.yml (pwsh), publish/publish-release.ps1 (run via publish-release.bat; prefers pwsh).
#
# Compression uses .NET GZipStream(Optimal). Windows PowerShell 5.1 (.NET Framework) and PowerShell 7+ (.NET)
# can produce different compressed sizes for the same plaintext; CI uses pwsh — run this script under pwsh
# for byte-identical output to the pipeline.
#
# Plaintext is gitignored; CI has no upload_text_policy.txt. If the .txt is missing but a committed payload exists,
# we skip (build uses the committed encrypted payload). Locally, keep .txt gitignored and run this
# script after editing the wordlist to refresh the payload before commit.
#
# Legacy (XOR + .txt.gz): if plaintext is absent but upload_text_policy.txt.gz and upload_text_policy.xorkey exist,
# the script XOR-decrypts the .gz to raw gzip bytes, then encrypts with AES-GCM (one-time migration path).
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    Write-Warning "gzip-policy: running under Windows PowerShell 5.1; CI uses pwsh — gzip output may differ from GitHub Actions. Install PowerShell 7+ and use: pwsh -File `"$PSCommandPath`""
}

function Protect-GzipWithAesGcmV1 {
    param(
        [Parameter(Mandatory = $true)][byte[]]$Aes256Key,
        [Parameter(Mandatory = $true)][byte[]]$GzipBytes
    )
    if ($Aes256Key.Length -ne 32) { throw "upload_text_policy.symkey must be exactly 32 bytes." }

    $aes = [System.Security.Cryptography.AesGcm]::new($Aes256Key, 16)
    $nonce = New-Object byte[] 12
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($nonce)

    $cipher = New-Object byte[] $GzipBytes.Length
    $tag = New-Object byte[] 16
    $aes.Encrypt($nonce, $GzipBytes, $cipher, $tag, $null)

    $out = New-Object byte[] (1 + 12 + $cipher.Length + 16)
    $out[0] = 1
    [Array]::Copy($nonce, 0, $out, 1, 12)
    [Array]::Copy($cipher, 0, $out, 13, $cipher.Length)
    [Array]::Copy($tag, 0, $out, 13 + $cipher.Length, 16)
    return $out
}

$p = Join-Path $PSScriptRoot 'upload_text_policy.txt'
$keyPath = Join-Path $PSScriptRoot 'upload_text_policy.symkey'
$legacyGz = Join-Path $PSScriptRoot 'upload_text_policy.txt.gz'
$legacyXor = Join-Path $PSScriptRoot 'upload_text_policy.xorkey'
$outPayload = Join-Path $PSScriptRoot 'upload_text_policy.payload'

if (-not (Test-Path -LiteralPath $keyPath)) {
    throw "Missing symmetric key file: $keyPath (32 bytes). Create a random 32-byte file (see repository docs / gzip-policy header)."
}

$symKey = [System.IO.File]::ReadAllBytes($keyPath)
if ($symKey.Length -ne 32) {
    throw "upload_text_policy.symkey must be exactly 32 bytes (got $($symKey.Length))."
}

$gzBytes = $null
if (Test-Path -LiteralPath $p) {
    $bytes = [System.IO.File]::ReadAllBytes($p)
    $ms = New-Object System.IO.MemoryStream
    try {
        $gz = New-Object System.IO.Compression.GZipStream($ms, [System.IO.Compression.CompressionLevel]::Optimal)
        try { $gz.Write($bytes, 0, $bytes.Length) }
        finally { $gz.Dispose() }
        $gzBytes = $ms.ToArray()
    }
    finally { $ms.Dispose() }
}
elseif ((Test-Path -LiteralPath $legacyGz) -and (Test-Path -LiteralPath $legacyXor)) {
    Write-Host "gzip-policy: plaintext missing; migrating legacy XOR obfuscated .txt.gz to AES-GCM payload."
    $xorKey = [System.IO.File]::ReadAllBytes($legacyXor)
    if ($xorKey.Length -eq 0) { throw "Legacy XOR key is empty: $legacyXor" }
    $obf = [System.IO.File]::ReadAllBytes($legacyGz)
    for ($i = 0; $i -lt $obf.Length; $i++) {
        $obf[$i] = $obf[$i] -bxor $xorKey[$i % $xorKey.Length]
    }
    $gzBytes = $obf
}
else {
    if (Test-Path -LiteralPath $outPayload) {
        Write-Host "Skip gzip-policy: plaintext not present ($p); using existing $outPayload."
        exit 0
    }
    throw "Missing $p (and no legacy XOR pair or $outPayload to fall back on)."
}

$envelope = Protect-GzipWithAesGcmV1 -Aes256Key $symKey -GzipBytes $gzBytes
[System.IO.File]::WriteAllBytes($outPayload, $envelope)
Write-Host "Wrote $outPayload ($((Get-Item $outPayload).Length) bytes)"
