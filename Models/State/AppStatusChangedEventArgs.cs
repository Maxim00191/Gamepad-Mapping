using System;

namespace GamepadMapperGUI.Models.State;

public sealed class AppStatusChangedEventArgs : EventArgs
{
    public AppStatusChangedEventArgs(AppTargetingState state, string statusText)
    {
        State = state;
        StatusText = statusText;
    }

    public AppTargetingState State { get; }

    public string StatusText { get; }
}
