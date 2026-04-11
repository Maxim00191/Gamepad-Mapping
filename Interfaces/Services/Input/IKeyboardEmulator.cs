using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Injected keyboard output simulation (implementation may use Win32, Interception, etc.).
/// </summary>
public interface IKeyboardEmulator
{
    void Execute(OutputCommand command);
    Task ExecuteAsync(OutputCommand command, CancellationToken cancellationToken = default);

    void KeyDown(Key key);
    void KeyUp(Key key);
    void TapKey(Key key, int repeatCount = 1, int interKeyDelayMs = 0, int keyHoldMs = 30);
    Task TapKeyAsync(Key key, int repeatCount = 1, int interKeyDelayMs = 0, int keyHoldMs = 30, CancellationToken cancellationToken = default);
    /// <summary>Modifier keys down (in order), main key tap, modifiers up (reverse order).</summary>
    void TapKeyChord(IReadOnlyList<Key> modifiers, Key mainKey, int keyHoldMs = 30);
    Task TapKeyChordAsync(IReadOnlyList<Key> modifiers, Key mainKey, int keyHoldMs = 30, CancellationToken cancellationToken = default);
    void SendText(string text, int interCharDelayMs = 0);
    void TapKeys(IEnumerable<Key> keys);
}

