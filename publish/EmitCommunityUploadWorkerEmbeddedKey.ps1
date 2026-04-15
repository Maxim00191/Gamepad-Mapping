param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,
    [string] $ApiKeyBase64 = "",
    [string] $SigningKeyBase64 = ""
)

$dir = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$b64 = $ApiKeyBase64 ?? ""
if ($b64.Contains('"') -or $b64.Contains("`n") -or $b64.Contains("`r")) {
    throw "ApiKeyBase64 must not contain quotes or newlines."
}
$signingB64 = $SigningKeyBase64 ?? ""
if ($signingB64.Contains('"') -or $signingB64.Contains("`n") -or $signingB64.Contains("`r")) {
    throw "SigningKeyBase64 must not contain quotes or newlines."
}

$src = @"
#nullable enable

namespace GamepadMapperGUI.Services.Infrastructure;

internal static class CommunityUploadWorkerEmbeddedKey
{
    internal static string GetUploadWorkerApiKey()
    {
        if (string.IsNullOrEmpty(s_keyB64))
            return string.Empty;
        return global::System.Text.Encoding.UTF8.GetString(global::System.Convert.FromBase64String(s_keyB64));
    }

    internal static string GetUploadWorkerSigningKey()
    {
        if (string.IsNullOrEmpty(s_signingKeyB64))
            return string.Empty;
        return global::System.Text.Encoding.UTF8.GetString(global::System.Convert.FromBase64String(s_signingKeyB64));
    }

    private const string s_keyB64 = "$b64";
    private const string s_signingKeyB64 = "$signingB64";
}
"@

[System.IO.File]::WriteAllText($OutputPath, $src + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
