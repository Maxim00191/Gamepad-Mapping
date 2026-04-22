using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface IAppToastService
{
    event EventHandler<AppToastRequest?>? CurrentToastChanged;

    void Show(AppToastRequest request);
    void ShowError(string titleKey, string messageKey, params object[] args);
    void ShowInfo(string titleKey, string messageKey, params object[] args);
    void LogDebug(string message);

    void DismissCurrent();

    /// <summary>
    /// Called when the main window / application is shutting down; honors <see cref="AppToastRequest.InvokeOnClosedWhenExitingApplication"/>.
    /// </summary>
    void NotifyApplicationExiting();
}

