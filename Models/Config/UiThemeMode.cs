#nullable enable

using System;

namespace GamepadMapperGUI.Models;

public static class UiThemeMode
{
    public const string FollowSystem = "followSystem";
    public const string Light = "light";
    public const string Dark = "dark";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return FollowSystem;

        var v = value.Trim();
        if (v.Equals(Light, StringComparison.OrdinalIgnoreCase))
            return Light;
        if (v.Equals(Dark, StringComparison.OrdinalIgnoreCase))
            return Dark;
        if (v.Equals(FollowSystem, StringComparison.OrdinalIgnoreCase))
            return FollowSystem;
        return FollowSystem;
    }

    public static bool IsFollowSystem(string? value) =>
        string.Equals(Normalize(value), FollowSystem, StringComparison.Ordinal);

    public static bool ResolveToLight(string? storedValue, Func<bool> windowsUsesLightTheme)
    {
        return Normalize(storedValue) switch
        {
            Light => true,
            Dark => false,
            _ => windowsUsesLightTheme()
        };
    }
}
