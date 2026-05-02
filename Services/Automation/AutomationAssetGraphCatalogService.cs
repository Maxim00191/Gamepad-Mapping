#nullable enable

using System.Collections.Generic;
using System.IO;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationAssetGraphCatalogService : IAutomationAssetGraphCatalogService
{
    public IReadOnlyList<AutomationAssetGraphInfo> ListBundledGraphJsonFiles()
    {
        var root = AppPaths.GetAutomationAssetsDirectory();
        if (!Directory.Exists(root))
            return [];

        var list = new List<AutomationAssetGraphInfo>();
        foreach (var full in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, full);
            var label = Path.GetFileNameWithoutExtension(full);
            var parent = Path.GetDirectoryName(rel);
            if (!string.IsNullOrEmpty(parent))
                label = $"{label} ({parent})";
            list.Add(new AutomationAssetGraphInfo(rel, label, full));
        }

        list.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
        return list;
    }
}
