using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IUpdateInstallExecutorService
{
    UpdateInstallExecutionResult Execute(UpdateInstallRequest request);
}
