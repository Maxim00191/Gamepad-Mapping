using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Infrastructure;

public static class CommunityTemplateUploadPathConflictChecker
{
    public static HashSet<string> BuildPublishedPathSet(IReadOnlyList<CommunityTemplateInfo> index)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in index)
        {
            var folder = NormalizeCatalogFolder(t.CatalogFolder);
            var file = (t.FileName ?? string.Empty).Trim();
            if (folder.Length == 0 || file.Length == 0)
                continue;
            set.Add($"{folder}/{file}");
        }

        return set;
    }

    public static List<string> FindConflictingRelativePaths(
        IReadOnlyCollection<GameProfileTemplate> selectedTemplates,
        string catalogFolderForSave,
        IReadOnlySet<string> publishedPaths)
    {
        var catalog = NormalizeCatalogFolder(catalogFolderForSave);
        if (catalog.Length == 0)
            return [];

        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        var seenInSubmission = new HashSet<string>(StringComparer.Ordinal);

        foreach (var template in selectedTemplates)
        {
            var pid = (template.ProfileId ?? string.Empty).Trim();
            if (pid.Length == 0)
                continue;

            var rel = $"{catalog}/{pid}.json";
            if (!seenInSubmission.Add(rel))
            {
                conflicts.Add(rel);
                continue;
            }

            if (publishedPaths.Contains(rel))
                conflicts.Add(rel);
        }

        return conflicts.Order(StringComparer.Ordinal).ToList();
    }

    private static string NormalizeCatalogFolder(string? raw)
    {
        var segments = TemplateStorageKey.SplitCatalogPathSegments(raw ?? string.Empty);
        if (segments.Count == 0)
            return string.Empty;
        return string.Join(TemplateStorageKey.Separator, segments);
    }
}
