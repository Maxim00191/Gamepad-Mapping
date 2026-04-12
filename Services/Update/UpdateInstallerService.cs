using System;
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

public sealed class UpdateInstallerService : IUpdateInstallerService
{
    private readonly IUpdateInstallExecutorService _executorService;

    public UpdateInstallerService(IUpdateInstallExecutorService? executorService = null)
    {
        _executorService = executorService ?? new UpdateInstallExecutorService();
    }

    public bool TryLaunchInstaller(UpdateInstallRequest request, out string? errorMessage)
    {
        var result = _executorService.Execute(request);
        errorMessage = result.ErrorMessage;
        return result.Succeeded;
    }
}


