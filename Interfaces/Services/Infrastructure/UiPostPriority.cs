namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

/// <summary>
/// Maps to WPF <see cref="System.Windows.Threading.DispatcherPriority"/> for work queued from the input/polling thread.
/// Prefer <see cref="Background"/> for labels and HUD chrome so gameplay keeps the UI thread responsive when the window is obscured or under load.
/// </summary>
public enum UiPostPriority
{
    /// <summary>WPF <see cref="System.Windows.Threading.DispatcherPriority.Normal"/>.</summary>
    Normal = 0,

    /// <summary>WPF <see cref="System.Windows.Threading.DispatcherPriority.Background"/>.</summary>
    Background = 1,

    /// <summary>WPF <see cref="System.Windows.Threading.DispatcherPriority.ContextIdle"/>.</summary>
    ContextIdle = 2
}
