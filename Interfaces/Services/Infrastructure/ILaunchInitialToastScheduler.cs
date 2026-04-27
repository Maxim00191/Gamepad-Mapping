#nullable enable

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

/// <summary>
/// Presents first-run and update-related corner toasts once the main window is loaded.
/// </summary>
public interface ILaunchInitialToastScheduler
{
    void ScheduleInitial();
}
