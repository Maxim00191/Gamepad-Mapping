using System;

namespace GamepadMapperGUI.Models.Core;

public sealed record CachedUpdateVersionInfo(
    string Owner,
    string Repo,
    string LatestVersion,
    string? ReleaseUrl,
    DateTimeOffset CachedAtUtc);
