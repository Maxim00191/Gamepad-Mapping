using System;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Core.Emulation.Noise;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Core.Emulation;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Input;

/// <summary>
/// Default stack factory: backend from <see cref="InputEmulationServices"/> with human-noise on mouse movement and keyboard tap hold.
/// <see cref="HumanInputNoiseParameters.Enabled"/> is read at runtime via <paramref name="getNoiseParameters"/> so toggling noise does not require rebuilding the mapping stack.
/// </summary>
public sealed class InputEmulationStackFactory : IInputEmulationStackFactory
{
    private readonly Func<int> _noiseSeed;
    private readonly ITimeProvider _timeProvider;

    public InputEmulationStackFactory(Func<int>? noiseSeed = null, ITimeProvider? timeProvider = null)
    {
        _noiseSeed = noiseSeed ?? (() => Random.Shared.Next());
        _timeProvider = timeProvider ?? new RealTimeProvider();
    }

    public (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) CreatePair(
        string? inputEmulationApiId,
        Func<HumanInputNoiseParameters> getNoiseParameters)
    {
        var (keyboard, mouse) = InputEmulationServices.CreatePair(inputEmulationApiId);

        INoiseGenerator noiseGen = new NoiseGenerator(_noiseSeed());
        var controller = new HumanInputNoiseController(noiseGen, getNoiseParameters, _timeProvider);
        return (
            new HumanizingKeyboardEmulator(keyboard, controller),
            new HumanizingMouseEmulator(mouse, controller));
    }
}
