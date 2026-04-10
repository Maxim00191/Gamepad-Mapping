using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IAppToastService
{
    event EventHandler<AppToastRequest?>? CurrentToastChanged;

    void Show(AppToastRequest request);

    void DismissCurrent();

    /// <summary>
    /// Called when the main window / application is shutting down; honors <see cref="AppToastRequest.InvokeOnClosedWhenExitingApplication"/>.
    /// </summary>
    void NotifyApplicationExiting();
}
