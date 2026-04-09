using System;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Services;

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
