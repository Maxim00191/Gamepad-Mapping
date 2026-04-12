#nullable enable

using System;
using System.Collections.Generic;

namespace GamepadMapperGUI.Utils;

/// <summary>Immutable-style updates for per-culture string maps in profile JSON.</summary>
internal static class LocalizedCultureStringMap
{
    public static string Get(IReadOnlyDictionary<string, string>? map, string culture)
    {
        if (map is null || string.IsNullOrEmpty(culture))
            return string.Empty;
        return map.TryGetValue(culture, out var v) ? v : string.Empty;
    }

    /// <summary>Returns a new map with <paramref name="culture"/> set or removed; <c>null</c> when the map would be empty.</summary>
    public static Dictionary<string, string>? WithCulture(Dictionary<string, string>? map, string culture, string? rawValue)
    {
        var trimmed = (rawValue ?? string.Empty).Trim();
        var next = map is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);

        if (trimmed.Length == 0)
        {
            next.Remove(culture);
            return next.Count == 0 ? null : next;
        }

        next[culture] = trimmed;
        return next;
    }

    public static bool ContentEquals(Dictionary<string, string>? a, Dictionary<string, string>? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null || a.Count != b.Count)
            return false;

        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var bv) || !string.Equals(kv.Value, bv, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
