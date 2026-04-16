#nullable enable

using System.Collections.Generic;
using System.IO;
using GamepadMapperGUI.Models.Core.Community;

namespace GamepadMapperGUI.Services.Infrastructure;

internal static class UploadTextPolicyTextParser
{
    internal static List<UploadTextPolicyPattern> Parse(string text)
    {
        var list = new List<UploadTextPolicyPattern>();
        if (string.IsNullOrEmpty(text))
            return list;

        using var reader = new StringReader(text.TrimStart('\ufeff'));
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var t = line.Trim();
            if (t.Length == 0 || t[0] == '#')
                continue;

            var parts = t.Split('\t', 3, StringSplitOptions.None);
            if (parts.Length < 3)
                continue;

            list.Add(new UploadTextPolicyPattern
            {
                Id = parts[0].Trim(),
                Mode = parts[1].Trim(),
                Match = parts[2].TrimEnd('\r')
            });
        }

        return list;
    }
}
