#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace GamepadMapperGUI.Core;

public static class GamepadThumbstickFromValueCatalog
{
    public static IReadOnlyList<string> PickList { get; } =
    [
        "UP",
        "DOWN",
        "LEFT",
        "RIGHT",
        "X",
        "Y",
        "MAGNITUDE"
    ];

    public static bool IsRecognizedStickInput(string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        AnalogProcessor.TryParseAnalogSource(token, out _);

    public static string CanonicalizeForEditor(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return PickList[0];

        var trimmed = stored.Trim();
        var pickHit = PickList.FirstOrDefault(p =>
            string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase));
        if (pickHit is not null)
            return pickHit;

        if (IsRecognizedStickInput(trimmed))
            return trimmed.ToUpperInvariant();

        return trimmed;
    }
}
