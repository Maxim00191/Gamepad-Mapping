using System;
using System.IO;
using System.Text;
using System.Text.Json;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services;

public sealed class UpdateVersionCacheService : IUpdateVersionCacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public void SaveLatestVersion(string owner, string repo, string latestVersion, string? releaseUrl)
    {
        if (string.IsNullOrWhiteSpace(owner) ||
            string.IsNullOrWhiteSpace(repo) ||
            string.IsNullOrWhiteSpace(latestVersion))
        {
            return;
        }

        try
        {
            var cache = new UpdateVersionCacheState
            {
                Owner = owner.Trim(),
                Repo = repo.Trim(),
                LatestVersion = latestVersion.Trim(),
                ReleaseUrl = string.IsNullOrWhiteSpace(releaseUrl) ? null : releaseUrl.Trim(),
                CachedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var filePath = AppPaths.GetUpdateVersionCacheFilePath();
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(cache, SerializerOptions);
            File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // Cache failures should never break update checks.
        }
    }

    public CachedUpdateVersionInfo? TryGetLatestVersion(string owner, string repo)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            return null;

        try
        {
            var filePath = AppPaths.GetUpdateVersionCacheFilePath();
            if (!File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var state = JsonSerializer.Deserialize<UpdateVersionCacheState>(json);
            if (state is null)
                return null;

            var cachedOwner = state.Owner?.Trim() ?? string.Empty;
            var cachedRepo = state.Repo?.Trim() ?? string.Empty;
            var cachedVersion = state.LatestVersion?.Trim() ?? string.Empty;
            if (!string.Equals(cachedOwner, owner.Trim(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(cachedRepo, repo.Trim(), StringComparison.OrdinalIgnoreCase) ||
                cachedVersion.Length == 0)
            {
                return null;
            }

            var cachedAtUtc = state.CachedAtUnixSeconds > 0
                ? DateTimeOffset.FromUnixTimeSeconds(state.CachedAtUnixSeconds)
                : DateTimeOffset.MinValue;

            return new CachedUpdateVersionInfo(
                cachedOwner,
                cachedRepo,
                cachedVersion,
                string.IsNullOrWhiteSpace(state.ReleaseUrl) ? null : state.ReleaseUrl.Trim(),
                cachedAtUtc);
        }
        catch
        {
            return null;
        }
    }
}
