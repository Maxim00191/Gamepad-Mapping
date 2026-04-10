namespace GamepadMapperGUI.Models;

public sealed class AppToastRequest
{
    public required string Title { get; init; }

    public string? Message { get; init; }

    /// <summary>
    /// Auto-hide duration: <c>null</c> = default (15s), <c>0</c> = no auto-hide, &gt; 0 = seconds.
    /// </summary>
    public int? AutoHideSeconds { get; init; }

    public Action? OnClosed { get; init; }

    /// <summary>
    /// When true, <see cref="OnClosed"/> is invoked if the application exits while this toast is still visible (e.g. update acknowledgement).
    /// </summary>
    public bool InvokeOnClosedWhenExitingApplication { get; init; }
}
