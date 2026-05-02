#nullable enable

using System.IO;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationNeedlePathResolver
{
    public static string? ResolveExistingFilePath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return null;

        var trimmed = rawPath.Trim();
        if (trimmed.Length == 0)
            return null;

        try
        {
            if (Path.IsPathRooted(trimmed))
            {
                var full = Path.GetFullPath(trimmed);
                return File.Exists(full) ? full : null;
            }
        }
        catch
        {
            return null;
        }

        try
        {
            var root = AppPaths.ResolveContentRoot();
            var relativeNormalized = trimmed.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var underRoot = Path.GetFullPath(Path.Combine(root, relativeNormalized));
            if (File.Exists(underRoot))
                return underRoot;

            var fileName = Path.GetFileName(trimmed);
            if (!string.IsNullOrEmpty(fileName))
            {
                var underCaptures = Path.Combine(AppPaths.GetAutomationCaptureCacheDirectory(), fileName);
                if (File.Exists(underCaptures))
                    return Path.GetFullPath(underCaptures);
            }
        }
        catch
        {
            return null;
        }

        try
        {
            var cwdCandidate = Path.GetFullPath(trimmed);
            if (File.Exists(cwdCandidate))
                return cwdCandidate;
        }
        catch
        {
            return null;
        }

        return null;
    }
}
