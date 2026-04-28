using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models.Core.Input;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Resolves and creates configured gamepad input sources.
/// </summary>
public interface IGamepadSourceFactory
{
    IReadOnlyList<GamepadSourceRegistration> GetRegistrations();

    string NormalizeApiId(string? apiId);

    IGamepadSource CreateSource(string? requestedApiId, out string resolvedApiId);
}
