using System.Windows.Input;

namespace GamepadMapperGUI.Models;

public readonly record struct DispatchedOutput(Key? KeyboardKey, PointerAction? PointerAction);

public readonly record struct QueuedOutputWork(
    string ButtonName,
    TriggerMoment Trigger,
    string OutputLabel,
    string SourceToken,
    DispatchedOutput? DirectOutput,
    System.Windows.Input.Key[]? ChordModifiers,
    System.Windows.Input.Key? ChordMainKey);

public enum PointerAction
{
    LeftClick,
    RightClick,
    MiddleClick,
    X1Click,
    X2Click,
    WheelUp,
    WheelDown
}
