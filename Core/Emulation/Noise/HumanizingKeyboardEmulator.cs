using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Services.Input;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>
/// Decorates any <see cref="IKeyboardEmulator"/> with human-noise on timed paths; forwards chord/text through the inner so backend sequencing (e.g. Win32 chord gate) stays correct.
/// </summary>
public sealed class HumanizingKeyboardEmulator : IKeyboardEmulator
{
    private readonly IKeyboardEmulator _inner;
    private readonly IHumanInputNoiseController _noise;

    private const int DefaultTapHoldMs = 30;
    private const int MinTapHoldMs = 20;
    private const int MaxTapHoldMs = 50;

    public HumanizingKeyboardEmulator(IKeyboardEmulator inner, IHumanInputNoiseController noise)
    {
        _inner = inner;
        _noise = noise;
    }

    public void KeyDown(Key key) => _inner.KeyDown(key);

    public void KeyUp(Key key) => _inner.KeyUp(key);

    public void TapKey(Key key, int repeatCount = 1, int interKeyDelayMs = 0, int keyHoldMs = DefaultTapHoldMs)
    {
        ValidateRepeatCount(repeatCount);
        var effectiveHoldMs = ClampHoldTime(keyHoldMs);

        for (var i = 0; i < repeatCount; i++)
        {
            _inner.KeyDown(key);
            var hold = _noise.AdjustDelayMs(effectiveHoldMs);
            if (hold > 0) Thread.Sleep(hold);
            _inner.KeyUp(key);

            if (interKeyDelayMs > 0 && i < repeatCount - 1)
            {
                var gap = _noise.AdjustDelayMs(interKeyDelayMs);
                if (gap > 0) Thread.Sleep(gap);
            }
        }
    }

    public async Task TapKeyAsync(
        Key key,
        int repeatCount = 1,
        int interKeyDelayMs = 0,
        int keyHoldMs = DefaultTapHoldMs,
        CancellationToken cancellationToken = default)
    {
        ValidateRepeatCount(repeatCount);
        var effectiveHoldMs = ClampHoldTime(keyHoldMs);

        for (var i = 0; i < repeatCount; i++)
        {
            _inner.KeyDown(key);
            await Task.Delay(_noise.AdjustDelayMs(effectiveHoldMs), cancellationToken).ConfigureAwait(false);
            _inner.KeyUp(key);

            if (interKeyDelayMs > 0 && i < repeatCount - 1)
                await Task.Delay(_noise.AdjustDelayMs(interKeyDelayMs), cancellationToken).ConfigureAwait(false);
        }
    }

    public void TapKeyChord(IReadOnlyList<Key> modifiers, Key mainKey, int keyHoldMs = DefaultTapHoldMs)
    {
        var noisyHold = NoisyChordHold(keyHoldMs);
        _inner.TapKeyChord(modifiers, mainKey, noisyHold);
    }

    public Task TapKeyChordAsync(
        IReadOnlyList<Key> modifiers,
        Key mainKey,
        int keyHoldMs = DefaultTapHoldMs,
        CancellationToken cancellationToken = default)
    {
        var noisyHold = NoisyChordHold(keyHoldMs);
        return _inner.TapKeyChordAsync(modifiers, mainKey, noisyHold, cancellationToken);
    }

    public void SendText(string text, int interCharDelayMs = 0)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));

        for (var i = 0; i < text.Length; i++)
        {
            _inner.SendText(text[i].ToString(), 0);
            if (interCharDelayMs > 0 && i < text.Length - 1)
            {
                var gap = _noise.AdjustDelayMs(interCharDelayMs);
                if (gap > 0) Thread.Sleep(gap);
            }
        }
    }

    public void TapKeys(IEnumerable<Key> keys) => _inner.TapKeys(keys);

    private static void ValidateRepeatCount(int repeatCount)
    {
        if (repeatCount < 1)
            throw new ArgumentOutOfRangeException(nameof(repeatCount), "repeatCount must be >= 1.");
    }

    private static int ClampHoldTime(int ms) => Math.Clamp(ms, MinTapHoldMs, MaxTapHoldMs);

    private int NoisyChordHold(int keyHoldMs)
    {
        var clamped = ClampHoldTime(keyHoldMs);
        return Math.Clamp(_noise.AdjustDelayMs(clamped), MinTapHoldMs, MaxTapHoldMs);
    }
}
