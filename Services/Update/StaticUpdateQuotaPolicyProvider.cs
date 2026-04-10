using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Services.Update;

public sealed class StaticUpdateQuotaPolicyProvider : IUpdateQuotaPolicyProvider
{
    // Short-term hardened defaults. Keep centralized for future remote/signed policy migration.
    private static readonly UpdateQuotaPolicy DefaultPolicy = new(
        CheckCooldownSeconds: 12,
        CheckDailyLimit: 30,
        DownloadDailyLimit: 5);

    public UpdateQuotaPolicy GetCurrentPolicy() => DefaultPolicy;
}


