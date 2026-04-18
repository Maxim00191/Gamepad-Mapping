#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)][string] $TargetDir,
    [Parameter(Mandatory = $true)][string] $TargetFileName,
    [Parameter(Mandatory = $true)][string] $OutputConfigPath
)
$ErrorActionPreference = 'Stop'
$inPath = [System.IO.Path]::GetFullPath($TargetDir).TrimEnd('\', '/')
$outPath = [System.IO.Path]::Combine($inPath, 'obfuscated')
$modulePath = [System.IO.Path]::Combine($inPath, $TargetFileName)
$inEsc = [System.Security.SecurityElement]::Escape($inPath)
$outEsc = [System.Security.SecurityElement]::Escape($outPath)
$modEsc = [System.Security.SecurityElement]::Escape($modulePath)
# KeepPublicApi: preserves public entry points (e.g. UploadTextPolicyEmbeddedReader) for host apps.
# HideStrings: encodes string literals in IL (manifest resource names, messages). The embedded .gz payload
# is unchanged binary data; AES-GCM payload is separate from Obfuscar. Obfuscar does not remove embedded resources.
$xml = @"
<?xml version='1.0'?>
<Obfuscator>
  <Var name="InPath" value="$inEsc" />
  <Var name="OutPath" value="$outEsc" />
  <Var name="KeepPublicApi" value="true" />
  <Var name="HideStrings" value="true" />
  <Module file="$modEsc" />
</Obfuscator>
"@
$dir = [System.IO.Path]::GetDirectoryName($OutputConfigPath)
if (-not [string]::IsNullOrEmpty($dir) -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutputConfigPath, $xml.TrimStart(), $utf8NoBom)
