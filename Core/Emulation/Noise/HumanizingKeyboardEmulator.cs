using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>
/// Decorates <see cref="IKeyboardEmulator"/> with human noise on tap hold duration only; nominal <c>keyHoldMs</c> comes from callers.
/// </summary>
public sealed class HumanizingKeyboardEmulator : IKeyboardEmulator
{
    private readonly IKeyboardEmulator _inner;
    private readonly IHumanInputNoiseController _noise;

    private const int TapHoldDeviationMs = 10;
    private const int DefaultTapHoldMs = 70;

    public HumanizingKeyboardEmulator(IKeyboardEmulator inner, IHumanInputNoiseController noise)
    {
        _inner = inner;
        _noise = noise;
    }

    private int AdjustHold(int keyHoldMs) => _noise.AdjustTapHoldMs(keyHoldMs, TapHoldDeviationMs);

    public void Execute(OutputCommand command)
    {
        if (command.Type == OutputCommandType.KeyTap)
        {
            int nominal = command.Metadata > 0 ? command.Metadata : DefaultTapHoldMs;
            _inner.Execute(command with { Metadata = AdjustHold(nominal) });
            return;
        }

        _inner.Execute(command);
    }

    public Task ExecuteAsync(OutputCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Type == OutputCommandType.KeyTap)
        {
            int nominal = command.Metadata > 0 ? command.Metadata : DefaultTapHoldMs;
            return _inner.ExecuteAsync(command with { Metadata = AdjustHold(nominal) }, cancellationToken);
        }

        return _inner.ExecuteAsync(command, cancellationToken);
    }

    public void KeyDown(Key key) => _inner.KeyDown(key);

    public void KeyUp(Key key) => _inner.KeyUp(key);

    public void TapKey(Key key, int repeatCount = 1, int interKeyDelayMs = 0, int keyHoldMs = DefaultTapHoldMs) =>
        _inner.TapKey(key, repeatCount, interKeyDelayMs, AdjustHold(keyHoldMs));

    public Task TapKeyAsync(
        Key key,
        int repeatCount = 1,
        int interKeyDelayMs = 0,
        int keyHoldMs = DefaultTapHoldMs,
        CancellationToken cancellationToken = default) =>
        _inner.TapKeyAsync(key, repeatCount, interKeyDelayMs, AdjustHold(keyHoldMs), cancellationToken);

    public void TapKeyChord(IReadOnlyList<Key> modifiers, Key mainKey, int keyHoldMs = DefaultTapHoldMs) =>
        _inner.TapKeyChord(modifiers, mainKey, AdjustHold(keyHoldMs));

    public Task TapKeyChordAsync(
        IReadOnlyList<Key> modifiers,
        Key mainKey,
        int keyHoldMs = DefaultTapHoldMs,
        CancellationToken cancellationToken = default) =>
        _inner.TapKeyChordAsync(modifiers, mainKey, AdjustHold(keyHoldMs), cancellationToken);

    public void SendText(string text, int interCharDelayMs = 0) => _inner.SendText(text, interCharDelayMs);

    public void TapKeys(IEnumerable<Key> keys) => _inner.TapKeys(keys);
}
