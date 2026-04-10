using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

internal interface IActionSession : IActiveAction
{
    /// <summary>The chord buttons that triggered this action and should be suppressed on release.</summary>
    IReadOnlySet<GamepadButtons> ActiveChord { get; }
}

