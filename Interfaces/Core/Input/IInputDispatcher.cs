using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IInputDispatcher
{
    void Enqueue(
        string buttonName,
        TriggerMoment trigger,
        DispatchedOutput output,
        string outputLabel,
        string sourceToken);

    void EnqueueChordTap(
        string buttonName,
        TriggerMoment trigger,
        Key[] modifiers,
        Key mainKey,
        string outputLabel,
        string sourceToken);

    Task WaitForIdleAsync();
    void Dispose();
}
