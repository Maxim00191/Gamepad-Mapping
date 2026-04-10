namespace GamepadMapperGUI.Models.State;

public sealed class UpdateSecurityState
{
    public string? HighestTrustedReleaseTag { get; set; }
    public long UpdatedAtUnixSeconds { get; set; }
}
