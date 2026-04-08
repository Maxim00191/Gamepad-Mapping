namespace GamepadMapperGUI.Models.State;

public sealed class UpdateVersionCacheState
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string? ReleaseUrl { get; set; }
    public long CachedAtUnixSeconds { get; set; }
}
