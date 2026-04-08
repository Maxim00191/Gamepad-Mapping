using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Services;

public sealed class StaticUpdateQuotaPolicyProvider : IUpdateQuotaPolicyProvider
{
    // Short-term hardened defaults. Keep centralized for future remote/signed policy migration.
    private static readonly UpdateQuotaPolicy DefaultPolicy = new(
        CheckCooldownSeconds: 12,
        CheckDailyLimit: 30,
        DownloadDailyLimit: 5);

    public UpdateQuotaPolicy GetCurrentPolicy() => DefaultPolicy;
}
