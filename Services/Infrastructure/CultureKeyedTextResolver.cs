using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Resolves strings from culture-keyed dictionaries (e.g. template JSON <c>descriptions</c> maps).
/// </summary>
public static class CultureKeyedTextResolver
{
    /// <summary>
    /// Picks a value for <paramref name="culture"/> and its parent chain, then the <c>default</c> key.
    /// Keys are matched case-insensitively; empty/whitespace values are ignored.
    /// </summary>
    public static bool TryPickForUiCulture(
        IReadOnlyDictionary<string, string>? map,
        CultureInfo culture,
        out string value)
    {
        value = string.Empty;
        if (map is null || map.Count == 0)
            return false;

        for (var c = culture; c is not null && !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            if (TryGetNonWhitespaceValue(map, c.Name, out value))
                return true;
        }

        return TryGetNonWhitespaceValue(map, "default", out value);
    }

    /// <summary>
    /// Deterministic fallback when the UI culture has no usable entry: first non-whitespace value sorted by key.
    /// </summary>
    public static bool TryPickFirstNonWhitespace(
        IReadOnlyDictionary<string, string>? map,
        out string value)
    {
        value = string.Empty;
        if (map is null || map.Count == 0)
            return false;

        foreach (var kv in map.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
                continue;
            value = kv.Value.Trim();
            return true;
        }

        return false;
    }

    private static bool TryGetNonWhitespaceValue(
        IReadOnlyDictionary<string, string> map,
        string key,
        out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        foreach (var kv in map)
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
                continue;
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value.Trim();
                return true;
            }
        }

        return false;
    }
}
