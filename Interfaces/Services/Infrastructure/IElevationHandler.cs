using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface IElevationHandler
{
    bool IsBlockedByUipi(ProcessInfo target);

    /// <summary>
    /// Prompts to relaunch elevated when UIPI would block input. Returns true if a relaunch was started and the caller should skip further UI updates.
    /// </summary>
    bool CheckAndPromptElevation(ProcessInfo target);
}

