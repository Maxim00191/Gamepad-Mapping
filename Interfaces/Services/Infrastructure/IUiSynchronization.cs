#nullable enable

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

/// <summary>
/// Marshals work to the UI thread. <see cref="Post"/> is asynchronous (queued);
/// <see cref="Send"/> runs the callback before returning to the caller (used for teardown that must finish before disposal continues).
/// </summary>
public interface IUiSynchronization
{
    void Post(Action action);
    void Send(Action action);
}
