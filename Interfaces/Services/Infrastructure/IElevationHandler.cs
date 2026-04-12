using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface IElevationHandler
{
    bool IsBlockedByUipi(ProcessInfo target);
    void CheckAndPromptElevation(ProcessInfo target);
}

