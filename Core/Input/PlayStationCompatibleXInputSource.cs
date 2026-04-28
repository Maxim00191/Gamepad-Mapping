using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Input;

/// <summary>
/// Compatibility bridge to the native-first PlayStation source.
/// </summary>
public sealed class PlayStationCompatibleXInputSource(
    IPlayStationInputProvider playStationInputProvider,
    IXInput xinput,
    uint userIndex = 0) : IGamepadSource
{
    private readonly PlayStationNativeSource _nativeSource = new(playStationInputProvider);
    private readonly XInputSource _xInputFallbackSource = new(xinput, userIndex);

    public bool TryGetFrame(out InputFrame frame)
    {
        if (_nativeSource.TryGetFrame(out frame))
            return true;

        return _xInputFallbackSource.TryGetFrame(out frame);
    }
}
