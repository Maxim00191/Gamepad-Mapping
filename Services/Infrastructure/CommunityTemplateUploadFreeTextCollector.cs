#nullable enable

using System.Collections.Generic;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core.Community;

namespace GamepadMapperGUI.Services.Infrastructure;

public static class CommunityTemplateUploadFreeTextCollector
{
    public static void CollectSubmissionFields(
        string gameFolderDisplayName,
        string authorDisplayName,
        string listingDescription,
        List<TextContentInspectionField> sink)
    {
        Add(sink, string.Empty, "Game folder name", gameFolderDisplayName);
        Add(sink, string.Empty, "Author folder name", authorDisplayName);
        Add(sink, string.Empty, "Listing description", listingDescription);
    }

    public static void CollectTemplateFields(
        GameProfileTemplate template,
        string templateContextLabel,
        List<TextContentInspectionField> sink)
    {
        Add(sink, templateContextLabel, "Profile id", template.ProfileId);
        Add(sink, templateContextLabel, "Template group id", template.TemplateGroupId);
        Add(sink, templateContextLabel, "Display name", template.DisplayName);
        Add(sink, templateContextLabel, "Display name key", template.DisplayNameKey);
        Add(sink, templateContextLabel, "Author", template.Author);
        Add(sink, templateContextLabel, "Community listing description", template.CommunityListingDescription);
        Add(sink, templateContextLabel, "Target process name", template.TargetProcessName);

        if (template.DisplayNames is not null)
        {
            foreach (var kv in template.DisplayNames)
                Add(sink, templateContextLabel, $"Display name ({kv.Key})", kv.Value);
        }

        if (template.KeyboardActions is not null)
        {
            foreach (var a in template.KeyboardActions)
            {
                Add(sink, templateContextLabel, $"Keyboard action ({a.Id}) id", a.Id);
                Add(sink, templateContextLabel, $"Keyboard action ({a.Id}) description", a.Description);
                Add(sink, templateContextLabel, $"Keyboard action ({a.Id}) description key", a.DescriptionKey);
                if (a.Descriptions is not null)
                {
                    foreach (var kv in a.Descriptions)
                        Add(sink, templateContextLabel, $"Keyboard action ({a.Id}) description ({kv.Key})", kv.Value);
                }
            }
        }

        if (template.RadialMenus is not null)
        {
            foreach (var rm in template.RadialMenus)
            {
                Add(sink, templateContextLabel, $"Radial menu ({rm.Id}) id", rm.Id);
                Add(sink, templateContextLabel, $"Radial menu ({rm.Id}) display name", rm.DisplayName);
                if (rm.DisplayNames is not null)
                {
                    foreach (var kv in rm.DisplayNames)
                        Add(sink, templateContextLabel, $"Radial menu ({rm.Id}) title ({kv.Key})", kv.Value);
                }

                foreach (var item in rm.Items)
                {
                    Add(sink, templateContextLabel, $"Radial menu ({rm.Id}) item action id", item.ActionId);
                    Add(sink, templateContextLabel, $"Radial menu ({rm.Id}) item label", item.Label);
                    if (item.Labels is not null)
                    {
                        foreach (var kv in item.Labels)
                            Add(sink, templateContextLabel, $"Radial menu ({rm.Id}) item label ({kv.Key})", kv.Value);
                    }

                    Add(sink, templateContextLabel, $"Radial menu ({rm.Id}) item icon", item.Icon);
                }
            }
        }

        for (var i = 0; i < template.Mappings.Count; i++)
        {
            var m = template.Mappings[i];
            var prefix = $"Mapping [{i + 1}]";
            Add(sink, templateContextLabel, $"{prefix} description", m.Description);
            Add(sink, templateContextLabel, $"{prefix} description key", m.DescriptionKey);
            if (m.Descriptions is not null)
            {
                foreach (var kv in m.Descriptions)
                    Add(sink, templateContextLabel, $"{prefix} description ({kv.Key})", kv.Value);
            }

            Add(sink, templateContextLabel, $"{prefix} action id", m.ActionId);
            Add(sink, templateContextLabel, $"{prefix} hold action id", m.HoldActionId);

            if (m.TemplateToggle is not null)
                Add(sink, templateContextLabel, $"{prefix} template toggle profile id", m.TemplateToggle.AlternateProfileId);

            if (m.RadialMenu is not null)
                Add(sink, templateContextLabel, $"{prefix} radial menu id", m.RadialMenu.RadialMenuId);

            if (m.ItemCycle is not null)
            {
                var ic = m.ItemCycle;
                Add(sink, templateContextLabel, $"{prefix} item cycle forward key", ic.LoopForwardKey);
                Add(sink, templateContextLabel, $"{prefix} item cycle backward key", ic.LoopBackwardKey);
                if (ic.WithKeys is not null)
                {
                    foreach (var k in ic.WithKeys)
                        Add(sink, templateContextLabel, $"{prefix} item cycle with-key", k);
                }
            }
        }
    }

    private static void Add(
        List<TextContentInspectionField> sink,
        string contextLabel,
        string fieldCaption,
        string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0)
            return;

        sink.Add(new TextContentInspectionField(contextLabel, fieldCaption, v));
    }
}
