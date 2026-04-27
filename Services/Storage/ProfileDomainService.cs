using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Storage;

public partial class ProfileDomainService : IProfileDomainService
{
    private static readonly Regex ValidIdPattern = new("^[a-zA-Z0-9][a-zA-Z0-9._-]*$", RegexOptions.Compiled);

    public IValidationResult ValidateTemplate(GameProfileTemplate template)
    {
        var validator = new ProfileValidator();
        return validator.Validate(template);
    }

    public string CreateUniqueProfileId(string templateGroupId, string? displayName, IEnumerable<string> existingIds)
    {
        var normalizedTemplateGroupId = (templateGroupId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTemplateGroupId))
            throw new ArgumentException("Template group ID is required.", nameof(templateGroupId));

        var displaySegment = SlugSegment(displayName);
        var baseId = string.IsNullOrWhiteSpace(displaySegment)
            ? normalizedTemplateGroupId
            : $"{normalizedTemplateGroupId}__{displaySegment}";

        var existingSet = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingSet.Contains(baseId))
            return baseId;

        var index = 2;
        while (true)
        {
            var candidate = $"{baseId}-{index}";
            if (!existingSet.Contains(candidate))
                return candidate;
            index++;
        }
    }

    public string EnsureUniqueId(string? preferred, IEnumerable<string?> existing, string fallbackPrefix)
    {
        var p = (preferred ?? string.Empty).Trim();
        if (p.Length == 0) p = fallbackPrefix;

        var existingSet = existing.Where(id => id != null).Select(id => id!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingSet.Contains(p)) return p;

        for (int i = 2; i < 10000; i++)
        {
            var candidate = $"{p}_{i}";
            if (!existingSet.Contains(candidate)) return candidate;
        }
        return $"{p}_{Guid.NewGuid():N}"[..12];
    }

    public bool IsValidId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        return ValidIdPattern.IsMatch(id.Trim());
    }

    private static string SlugSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        var collapsed = MyRegex().Replace(new string(chars), "-").Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? string.Empty : collapsed;
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex MyRegex();
}
