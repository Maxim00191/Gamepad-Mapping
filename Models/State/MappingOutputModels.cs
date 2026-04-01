using System.Windows.Input;

namespace GamepadMapperGUI.Models;

internal readonly record struct DispatchedOutput(Key? KeyboardKey, PointerAction? PointerAction);

internal readonly record struct QueuedOutputWork(
    string ButtonName,
    TriggerMoment Trigger,
    string OutputLabel,
    string SourceToken,
    DispatchedOutput? DirectOutput,
    System.Windows.Input.Key[]? ChordModifiers,
    System.Windows.Input.Key? ChordMainKey);

internal enum PointerAction
{
    LeftClick,
    RightClick,
    MiddleClick,
    X1Click,
    X2Click,
    WheelUp,
    WheelDown
}
