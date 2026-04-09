using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Utils;

public static class AppPaths
{
    public static string ResolveContentRoot()
    {
        // In dev, current directory is often the project root.
        // In debug/release builds, BaseDirectory points to bin/<tfm>/... so we need to walk up.
        var markerFile = Path.Combine("Assets", "Config", "default_settings.json");

        var candidates = new List<string>();

        try
        {
            candidates.Add(Directory.GetCurrentDirectory());
        }
        catch
        {
            // ignore
        }

        try
        {
            candidates.Add(AppContext.BaseDirectory);

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 10; i++)
            {
                if (dir.Parent is null) break;
                candidates.Add(dir.Parent.FullName);
                dir = dir.Parent;
            }
        }
        catch
        {
            // ignore
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var markerPath = Path.Combine(candidate, markerFile);
                if (File.Exists(markerPath))
                    return candidate;
            }
            catch
            {
                // ignore
            }
        }

        // Fallback: current directory (works for local runs).
        return Directory.GetCurrentDirectory();
    }

    /// <summary>OS path layout under <c>Assets/Profiles/templates</c>; logical keys live in <see cref="TemplateStorageKey"/>.</summary>
    public static class TemplateCatalogPaths
    {
        public static string BuildStorageKey(string? catalogSubfolder, string fileStem) =>
            TemplateStorageKey.Format(catalogSubfolder, fileStem);

        public static string NormalizeTrustedCatalogSubfolder(string? catalogSubfolder) =>
            TemplateStorageKey.NormalizeTrustedFolder(catalogSubfolder);

        public static bool TryParseStorageKey(string raw, out string? catalogSubfolder, out string fileStem) =>
            TemplateStorageKey.TryParse(raw, out catalogSubfolder, out fileStem);

        public static string GetTemplateJsonPath(string templatesRoot, string? catalogSubfolder, string fileStem)
        {
            var folder = TemplateStorageKey.NormalizeTrustedFolder(catalogSubfolder);
            var dir = folder.Length == 0 ? templatesRoot : Path.Combine(templatesRoot, folder);
            return Path.Combine(dir, $"{fileStem.Trim()}.json");
        }

        public static string NormalizeCatalogSubfolderForSave(string? raw) =>
            TemplateStorageKey.ValidateSingleSegmentFolderForSave(raw);
    }

    public static string GetLogsDirectory()
    {
        var root = ResolveContentRoot();
        var logsDir = Path.Combine(root, "Logs");
        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }
        return logsDir;
    }

    public static string GetUpdateDownloadsDirectory()
    {
        var root = ResolveContentRoot();
        var updateDir = Path.Combine(root, "Updates");
        if (!Directory.Exists(updateDir))
        {
            Directory.CreateDirectory(updateDir);
        }

        return updateDir;
    }

    public static string GetUpdateQuotaStateFilePath()
    {
        var updatesDir = GetUpdateDownloadsDirectory();
        return Path.Combine(updatesDir, "update-quota.dat");
    }

    public static string GetUpdateVersionCacheFilePath()
    {
        var updatesDir = GetUpdateDownloadsDirectory();
        return Path.Combine(updatesDir, "latest-version-cache.json");
    }

    public static string GetUpdateSecurityStateFilePath()
    {
        var updatesDir = GetUpdateDownloadsDirectory();
        return Path.Combine(updatesDir, "update-security-state.json");
    }
}

