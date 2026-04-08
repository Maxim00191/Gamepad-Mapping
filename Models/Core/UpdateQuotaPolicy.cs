namespace GamepadMapperGUI.Models.Core;

public sealed record UpdateQuotaPolicy(
    int CheckCooldownSeconds,
    int CheckDailyLimit,
    int DownloadDailyLimit);
