using System.Windows.Input;

namespace GamepadMapperGUI.Models;

public enum PointerAction
{
    None,
    LeftClick,
    RightClick,
    MiddleClick,
    X1Click,
    X2Click,
    WheelUp,
    WheelDown
}

public enum OutputCommandType
{
    None,
    KeyPress,
    KeyRelease,
    KeyTap,
    PointerDown,
    PointerUp,
    PointerClick,
    PointerWheel,
    Text
}

/// <summary>
/// A backend-neutral representation of an output command.
/// </summary>
public readonly record struct OutputCommand(
    OutputCommandType Type,
    Key Key = Key.None,
    PointerAction PointerAction = PointerAction.None,
    string? Text = null,
    int Metadata = 0);

public readonly record struct DispatchedOutput(Key? KeyboardKey, PointerAction? PointerAction);

public readonly record struct QueuedOutputWork(
    string ButtonName,
    TriggerMoment Trigger,
    string OutputLabel,
    string SourceToken,
    DispatchedOutput? DirectOutput,
    System.Windows.Input.Key[]? ChordModifiers,
    System.Windows.Input.Key? ChordMainKey);
