using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IElevationHandler
{
    bool IsBlockedByUipi(ProcessInfo target);
    void CheckAndPromptElevation(ProcessInfo target);
}
