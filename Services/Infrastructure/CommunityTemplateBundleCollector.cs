using System;
using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class CommunityTemplateBundleCollector(IProfileService profileService)
{
    private readonly IProfileService _profileService = profileService;

    public IReadOnlyList<CommunityTemplateBundleEntry> CollectLinkedTemplates(string rootStorageKey)
    {
        var entries = new List<CommunityTemplateBundleEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(rootStorageKey.Trim());

        while (queue.Count > 0)
        {
            var keyOrId = queue.Dequeue().Trim();
            if (keyOrId.Length == 0)
                continue;

            if (!_profileService.TryResolveTemplateLocation(keyOrId, out var loc))
                continue;

            var storageKey = TemplateStorageKey.Format(loc.CatalogSubfolder, loc.FileStem);
            if (!seen.Add(storageKey))
                continue;

            var template = _profileService.LoadTemplate(storageKey);
            entries.Add(new CommunityTemplateBundleEntry(storageKey, template));

            foreach (var target in EnumerateTemplateToggleTargets(template))
            {
                var t = target.Trim();
                if (t.Length > 0)
                    queue.Enqueue(t);
            }
        }

        return entries;
    }

    private static IEnumerable<string> EnumerateTemplateToggleTargets(GameProfileTemplate template)
    {
        foreach (var m in template.Mappings)
        {
            var id = m.TemplateToggle?.AlternateProfileId;
            if (!string.IsNullOrWhiteSpace(id))
                yield return id!;
        }

        if (template.KeyboardActions is null)
            yield break;

        foreach (var a in template.KeyboardActions)
        {
            var id = a.TemplateToggle?.AlternateProfileId;
            if (!string.IsNullOrWhiteSpace(id))
                yield return id!;
        }
    }
}
