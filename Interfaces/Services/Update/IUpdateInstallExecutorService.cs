using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Interfaces.Services.Update;

public interface IUpdateInstallExecutorService
{
    UpdateInstallExecutionResult Execute(UpdateInstallRequest request);
}

