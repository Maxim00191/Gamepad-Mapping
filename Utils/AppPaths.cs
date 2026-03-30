using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
}

