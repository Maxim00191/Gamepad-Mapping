namespace GamepadMapperGUI.Services.Infrastructure;

internal static class CommunityUploadWorkerCredentials
{
    internal static string ResolveUploadWorkerApiKey(string? fromAppSettings)
    {
        var trimmed = (fromAppSettings ?? string.Empty).Trim();
        if (trimmed.Length > 0)
            return trimmed;

        return CommunityUploadWorkerEmbeddedKey.GetUploadWorkerApiKey();
    }
}
