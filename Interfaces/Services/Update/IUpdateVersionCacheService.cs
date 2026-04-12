using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services.Update;

public interface IUpdateVersionCacheService
{
    void SaveLatestVersion(string owner, string repo, string latestVersion, string? releaseUrl);
    CachedUpdateVersionInfo? TryGetLatestVersion(string owner, string repo);
}

