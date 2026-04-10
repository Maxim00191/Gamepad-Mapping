using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services.Update;

public interface IUpdateInstallerService
{
    bool TryLaunchInstaller(UpdateInstallRequest request, out string? errorMessage);
}

