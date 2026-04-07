using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IUpdateInstallerService
{
    bool TryLaunchInstaller(UpdateInstallRequest request, out string? errorMessage);
}
