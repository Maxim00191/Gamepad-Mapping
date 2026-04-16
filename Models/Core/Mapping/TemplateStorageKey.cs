#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GamepadMapperGUI.Models;

/// <summary>
/// Canonical <b>logical</b> identity for a template file (settings, gamepad toggle, UI selection)—not an OS path.
/// Uses <c>/</c> as the separator in persisted strings so the format stays stable across platforms.
/// Layout on disk is still decided by <see cref="GamepadMapperGUI.Utils.AppPaths.TemplateCatalogPaths.GetTemplateJsonPath"/>.
/// </summary>
public static class TemplateStorageKey
{
    public const char Separator = '/';

    public static string Format(string? catalogSubfolder, string fileStem)
    {
        var folder = NormalizeTrustedFolder(catalogSubfolder);
        var stem = (fileStem ?? string.Empty).Trim();
        return folder.Length == 0 ? stem : $"{folder}{Separator}{stem}";
    }

    /// <summary>Folder segment as read from disk or trusted JSON; trim only.</summary>
    public static string NormalizeTrustedFolder(string? catalogSubfolder)
        => (catalogSubfolder ?? string.Empty).Trim();

    /// <summary>Parses <c>stem</c> or <c>Folder/stem</c> (last slash splits folder from file stem).</summary>
    public static bool TryParse(string raw, out string? catalogSubfolder, out string fileStem)
    {
        catalogSubfolder = null;
        fileStem = string.Empty;
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0)
            return false;

        var idx = s.LastIndexOf(Separator);
        if (idx < 0)
            idx = s.LastIndexOf('\\');

        if (idx <= 0 || idx >= s.Length - 1)
        {
            fileStem = s;
            return fileStem.Length > 0;
        }

        catalogSubfolder = s[..idx].Trim();
        fileStem = s[(idx + 1)..].Trim();
        return fileStem.Length > 0 && !string.IsNullOrEmpty(catalogSubfolder);
    }

    public static string ValidateSingleSegmentFolderForSave(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0)
            return string.Empty;

        if (s.IndexOfAny(['/', '\\']) >= 0)
            throw new ArgumentException("Catalog folder must be a single name (no path separators).", nameof(raw));

        if (s is "." or "..")
            throw new ArgumentException("Catalog folder cannot be '.' or '..'.", nameof(raw));

        if (s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Catalog folder contains invalid characters.", nameof(raw));

        return s;
    }

    /// <summary>
    /// Validates a relative catalog path under the templates root (forward slashes in persisted JSON).
    /// Each segment must be a single path component (no nested separators).
    /// </summary>
    public static string ValidateCatalogFolderPathForSave(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0)
            return string.Empty;

        var segments = SplitCatalogPathSegments(s);
        if (segments.Count == 0)
            return string.Empty;

        foreach (var seg in segments)
            ValidateSingleSegmentFolderForSave(seg);

        return string.Join(Separator, segments);
    }

    public static IReadOnlyList<string> SplitCatalogPathSegments(string raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0)
            return [];

        return s.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static p => p.Trim())
            .Where(static p => p.Length > 0)
            .ToList();
    }
}
