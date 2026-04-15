using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.Core.Community;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services.Infrastructure;

public static class CommunityUploadBundleFingerprint
{
    public static string Compute(IReadOnlyList<CommunityTemplateBundleEntry> entries)
    {
        if (entries is null || entries.Count == 0)
            return string.Empty;

        var normalizedEntries = entries
            .Select(static entry => new
            {
                StorageKey = (entry.StorageKey ?? string.Empty).Trim(),
                TemplateJson = JsonConvert.SerializeObject(entry.Template, Formatting.None)
            })
            .OrderBy(static entry => entry.StorageKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = JsonConvert.SerializeObject(normalizedEntries, Formatting.None);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
