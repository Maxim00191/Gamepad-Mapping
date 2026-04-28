using System;
using System.Collections.Generic;
using GamepadMapperGUI.Core.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core.Input;

namespace GamepadMapperGUI.Services.Input;

public sealed class GamepadSourceFactory(IXInput xInput) : IGamepadSourceFactory
{
    private static readonly IReadOnlyList<GamepadSourceRegistration> Registrations =
    [
        new(GamepadSourceApiIds.XInput, "GamepadSourceXInputLabel", true),
        new(GamepadSourceApiIds.DualSense, "GamepadSourceDualSenseLabel", false)
    ];

    private readonly IXInput _xInput = xInput ?? throw new ArgumentNullException(nameof(xInput));

    public IReadOnlyList<GamepadSourceRegistration> GetRegistrations() => Registrations;

    public string NormalizeApiId(string? apiId)
    {
        if (string.IsNullOrWhiteSpace(apiId))
            return GamepadSourceApiIds.XInput;

        foreach (var registration in Registrations)
        {
            if (string.Equals(registration.Id, apiId.Trim(), StringComparison.OrdinalIgnoreCase))
                return registration.Id;
        }

        return GamepadSourceApiIds.XInput;
    }

    public IGamepadSource CreateSource(string? requestedApiId, out string resolvedApiId)
    {
        var normalizedApiId = NormalizeApiId(requestedApiId);
        if (string.Equals(normalizedApiId, GamepadSourceApiIds.DualSense, StringComparison.OrdinalIgnoreCase))
        {
            Gamepad_Mapping.App.Logger.Warning("DualSense source is not implemented yet. Falling back to XInput.");
            resolvedApiId = GamepadSourceApiIds.XInput;
            return new XInputSource(_xInput);
        }

        resolvedApiId = GamepadSourceApiIds.XInput;
        return new XInputSource(_xInput);
    }
}
