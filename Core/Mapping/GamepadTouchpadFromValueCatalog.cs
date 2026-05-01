#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

public static class GamepadTouchpadFromValueCatalog
{
    public static IReadOnlyList<string> PickList { get; } =
    [
        "MOUSEX",
        "MOUSEY",
        "SWIPE_UP",
        "SWIPE_DOWN",
        "SWIPE_LEFT",
        "SWIPE_RIGHT"
    ];

    public static GamepadBinding CreateDefaultSurfaceBinding() =>
        new()
        {
            Type = GamepadBindingType.Touchpad,
            Value = PickList[0]
        };

    public static bool IsRecognizedTouchpadInput(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var trimmed = token.Trim();
        return AnalogProcessor.TryResolveMouseLookOutput(trimmed, out _)
               || TryParseSwipe(trimmed, out _);
    }

    public static bool TryParseSwipe(string? token, out TouchpadSwipeDirection direction)
    {
        direction = default;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var normalized = token.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        switch (normalized)
        {
            case "SWIPEUP":
                direction = TouchpadSwipeDirection.Up;
                return true;
            case "SWIPEDOWN":
                direction = TouchpadSwipeDirection.Down;
                return true;
            case "SWIPELEFT":
                direction = TouchpadSwipeDirection.Left;
                return true;
            case "SWIPERIGHT":
                direction = TouchpadSwipeDirection.Right;
                return true;
            default:
                return false;
        }
    }

    public static string CanonicalizeForEditor(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return PickList[0];

        var trimmed = stored.Trim();
        var pickHit = PickList.FirstOrDefault(p =>
            string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase));
        if (pickHit is not null)
            return pickHit;

        if (AnalogProcessor.TryResolveMouseLookOutput(trimmed, out _))
            return trimmed.ToUpperInvariant();

        if (TryParseSwipe(trimmed, out _))
            return NormalizeSwipeToken(trimmed);

        return trimmed;
    }

    private static string NormalizeSwipeToken(string trimmed)
    {
        var normalized = trimmed.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        return normalized switch
        {
            "SWIPEUP" => "SWIPE_UP",
            "SWIPEDOWN" => "SWIPE_DOWN",
            "SWIPELEFT" => "SWIPE_LEFT",
            "SWIPERIGHT" => "SWIPE_RIGHT",
            _ => trimmed.ToUpperInvariant()
        };
    }
}
