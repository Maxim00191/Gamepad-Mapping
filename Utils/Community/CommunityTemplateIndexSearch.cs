using System;
using System.Collections.Generic;
using System.Text;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Utils.Community;

/// <summary>
/// Client-side filtering for community <c>index.json</c> entries. Keeps searchable fields in one place
/// so new index properties can be included without scattering string concatenation across the UI layer.
/// <see cref="CommunityTemplateInfo.DownloadUrl"/> is intentionally excluded (noisy, low user value for search).
/// </summary>
public static class CommunityTemplateIndexSearch
{
    private static readonly char[] WhitespaceSeparators = { ' ', '\t', '\r', '\n' };

    /// <summary>
    /// Returns templates whose concatenated index fields match every whitespace-separated term in <paramref name="query"/>
    /// (case-insensitive). An empty or whitespace-only query yields the full <paramref name="source"/> list.
    /// </summary>
    public static List<CommunityTemplateInfo> Filter(
        IReadOnlyList<CommunityTemplateInfo> source,
        string? query)
    {
        if (source.Count == 0)
            return [];

        var terms = SplitSearchTerms(query);
        if (terms.Count == 0)
            return [..source];

        var result = new List<CommunityTemplateInfo>(source.Count);
        foreach (var t in source)
        {
            if (MatchesTerms(t, terms))
                result.Add(t);
        }

        return result;
    }

    private static List<string> SplitSearchTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var terms = query.Trim().Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
            return [];

        var list = new List<string>(terms.Length);
        foreach (var t in terms)
        {
            var trimmed = t.Trim();
            if (trimmed.Length > 0)
                list.Add(trimmed);
        }

        return list;
    }

    private static bool MatchesTerms(CommunityTemplateInfo template, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
            return true;

        var haystack = BuildSearchHaystack(template);
        foreach (var term in terms)
        {
            if (haystack.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    private static string BuildSearchHaystack(CommunityTemplateInfo t)
    {
        var sb = new StringBuilder(128);

        void Append(string? s)
        {
            if (string.IsNullOrEmpty(s))
                return;
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(s);
        }

        Append(t.Id);
        Append(t.DisplayName);
        if (t.DisplayNames is { Count: > 0 })
        {
            foreach (var kv in t.DisplayNames)
                Append(kv.Value);
        }

        Append(t.Author);
        Append(t.Description);
        Append(t.CatalogFolder);
        Append(t.FileName);
        Append(t.RelativePath);

        if (t.Tags is { Count: > 0 })
        {
            foreach (var tag in t.Tags)
                Append(tag);
        }

        return sb.ToString();
    }
}
