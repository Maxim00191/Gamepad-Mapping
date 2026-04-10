using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Updater.Install;

internal static class UpdaterInstallPathPolicy
{
    private static readonly string[] ReservedPreservePaths =
    [
        ".git",
        ".vs",
        "Logs",
        "Updates"
    ];

    public static readonly string[] MergeTargetRelativePaths =
    [
        @"Assets\Config\default.json",
        @"Assets\Config\default_settings.json"
    ];

    public static IReadOnlyList<string> NormalizePreservePaths(IReadOnlyList<string>? rawPaths)
    {
        var result = new List<string>();

        if (rawPaths is not null)
        {
            foreach (var raw in rawPaths)
            {
                var normalized = ValidateAndNormalizeRelativePath(raw);
                if (normalized.Length > 0)
                    result.Add(normalized);
            }
        }

        foreach (var reserved in ReservedPreservePaths)
        {
            var normalized = ValidateAndNormalizeRelativePath(reserved);
            if (normalized.Length > 0)
                result.Add(normalized);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsSubPathOf(string candidatePath, string rootPath)
    {
        var candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string ValidateAndNormalizeRelativePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        var normalized = raw.Trim().Replace('/', '\\').Trim('\\');
        if (normalized.Length == 0)
            return string.Empty;
        if (Path.IsPathRooted(normalized))
            throw new InvalidOperationException($"Preserve path must be relative: {raw}");
        if (normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            throw new InvalidOperationException($"Preserve path cannot contain '..': {raw}");
        return normalized;
    }
}
