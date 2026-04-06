using System;
using System.Collections.Generic;
using System.Linq;
using Vortice.XInput;

namespace GamepadMapperGUI.Models;

/// <summary>
/// Ordered names for chord segments: digital <see cref="GamepadButtons"/> plus LeftTrigger/RightTrigger (matches core chord parsing).
/// </summary>
public static class GamepadChordSegmentCatalog
{
    public static IReadOnlyList<string> AllSegmentNames { get; } = Build();

    private static IReadOnlyList<string> Build()
    {
        var list = Enum.GetNames<GamepadButtons>()
            .Where(n => !string.Equals(n, nameof(GamepadButtons.None), StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        list.Add(nameof(GamepadBindingType.LeftTrigger));
        list.Add(nameof(GamepadBindingType.RightTrigger));
        return list;
    }
}
