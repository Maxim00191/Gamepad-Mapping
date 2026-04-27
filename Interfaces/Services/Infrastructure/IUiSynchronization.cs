#nullable enable

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

/// <summary>
/// Marshals work to the UI thread. <see cref="Post"/> is asynchronous (queued);
/// <see cref="Send"/> runs the callback before returning to the caller (used for teardown that must finish before disposal continues).
/// </summary>
public interface IUiSynchronization
{
    /// <inheritdoc cref="Post(Action, UiPostPriority)"/>
    void Post(Action action);

    /// <summary>Queues work on the UI dispatcher at the given priority.</summary>
    void Post(Action action, UiPostPriority priority);

    void Send(Action action);
}
