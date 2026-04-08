namespace GamepadMapperGUI.Models.State;

public sealed class UpdateQuotaState
{
    public string UtcDateKey { get; set; } = string.Empty;
    public int CheckCount { get; set; }
    public int DownloadCount { get; set; }
    public long LastCheckUnixSeconds { get; set; }
    public long LastObservedUnixSeconds { get; set; }
}
