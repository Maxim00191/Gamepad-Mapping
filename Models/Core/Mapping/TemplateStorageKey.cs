#nullable enable

using System;
using System.IO;

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

    /// <summary>
    /// Current product rule: one folder level under the templates root. Relaxing this check is the main knob for deeper trees later.
    /// </summary>
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
}
