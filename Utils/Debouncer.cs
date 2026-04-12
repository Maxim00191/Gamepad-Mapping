using System;
using System.Windows.Threading;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// A utility for debouncing actions, typically used for UI input.
/// </summary>
public class Debouncer
{
    private readonly DispatcherTimer _timer;
    private Action? _action;

    /// <summary>
    /// Initializes a new instance of the <see cref="Debouncer"/> class.
    /// </summary>
    /// <param name="interval">The debounce interval.</param>
    /// <param name="priority">The dispatcher priority.</param>
    public Debouncer(TimeSpan interval, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        _timer = new DispatcherTimer(priority)
        {
            Interval = interval
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Debounces the specified action. If called again before the interval elapses, the previous action is cancelled.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Debounce(Action action)
    {
        _action = action;
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>
    /// Immediately stops the timer and cancels any pending action.
    /// </summary>
    public void Cancel()
    {
        _timer.Stop();
        _action = null;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        _action?.Invoke();
        _action = null;
    }
}
