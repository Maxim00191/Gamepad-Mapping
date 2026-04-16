#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace GamepadMapperGUI.Utils.Text;

internal static class UploadFreeTextLinkDetector
{
    internal const string BlockedPatternId = "policy:block-urls-and-domains";

    internal const int MaxCharsForFullRegexScan = 262_144;

    private static readonly string[] ExplicitShortenerHosts =
    [
        "t.me",
        "bit.ly",
        "dwz.cn",
        "t.co",
        "suo.im",
        "url.cn",
        "pan.baidu",
        "pan.quark"
    ];

    private static readonly string[] BareTwoLetterAbuseTopLevelDomains =
    [
        "tk",
        "ga"
    ];

    private static readonly string[] BareDomainTopLevelDomains =
    [
        "travel", "museum", "aero", "jobs", "mobi", "name", "info", "arpa",
        "online", "solutions", "services", "network", "company", "business",
        "marketing", "support", "systems", "technology", "education", "community",
        "download", "review", "photos", "videos", "music", "games", "email",
        "social", "world", "today", "space", "store", "shop", "site", "tech",
        "wiki", "blog", "cloud", "host", "page", "link", "click", "news",
        "live", "fun", "club", "group", "life", "zone", "land", "team",
        "work", "media", "film", "photo", "video", "guru", "ninja", "rocks",
        "cool", "best", "top", "love", "chat", "forum", "board", "help",
        "town", "city", "pics",
        "win", "vip", "icu", "app",
        "com", "net", "org", "edu", "gov", "mil", "int", "biz", "pro", "xyz"
    ];

    private static readonly Regex Combined = Build();

    public static bool ContainsBlockedContent(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (ContainsExplicitShortenerHost(text))
            return true;

        if (text.Length > MaxCharsForFullRegexScan)
        {
            if (ContainsLikelyBlockedLinkLinear(text))
                return true;

            const int edge = 65_536;
            var head = text.AsSpan(0, edge);
            if (Combined.IsMatch(head))
                return true;

            var tail = text.AsSpan(text.Length - edge);
            return Combined.IsMatch(tail);
        }

        return Combined.IsMatch(text);
    }

    private static bool ContainsExplicitShortenerHost(string text)
    {
        foreach (var host in ExplicitShortenerHosts)
        {
            for (var idx = 0; idx < text.Length;)
            {
                idx = text.IndexOf(host, idx, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    break;

                if (idx == 0 || !IsAsciiLetterOrDigit(text[idx - 1]))
                    return true;

                idx++;
            }
        }

        return false;
    }

    private static bool IsAsciiLetterOrDigit(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');

    private static bool ContainsLikelyBlockedLinkLinear(string text)
    {
        var span = text.AsSpan();
        if (span.Contains("://".AsSpan(), StringComparison.OrdinalIgnoreCase))
            return true;
        if (span.Contains("www.".AsSpan(), StringComparison.OrdinalIgnoreCase))
            return true;
        if (span.Contains("hxxp".AsSpan(), StringComparison.OrdinalIgnoreCase))
            return true;
        if (span.Contains("mailto:".AsSpan(), StringComparison.OrdinalIgnoreCase))
            return true;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '@')
                continue;

            var dot = text.IndexOf('.', i + 1);
            if (dot < 0)
                continue;

            var spaceAfter = text.AsSpan(i + 1).IndexOfAny(" \t\r\n\v\f<>\"");
            var end = spaceAfter < 0 ? text.Length : i + 1 + spaceAfter;
            if (dot < end)
                return true;
        }

        return false;
    }

    private static Regex Build()
    {
        var tlds = BareDomainTopLevelDomains
            .Concat(BareTwoLetterAbuseTopLevelDomains)
            .Where(static s => s.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static s => s.Length)
            .ThenBy(static s => s, StringComparer.Ordinal);

        var tldAlternation = string.Join("|", tlds.Select(Regex.Escape));
        const string ipv4 =
            @"\b(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\b";

        var bareDomain =
            $@"\b(?:[a-z0-9](?:[a-z0-9-]{{0,61}}[a-z0-9])?\.)+(?:{tldAlternation})\b";

        var pattern = $"""
            (?ix)
            (?:\b(?:hxxps?|https?|wss?|ftp)://[^\s<>"']+)
            |(?:\b[a-z][a-z0-9+.-]*://[^\s<>"']+)
            |(?:\bwww\.[^\s<>"']+)
            |(?:[^\s<>"']+@[^\s<>"']+\.[^\s<>"']+)
            |(?:{bareDomain})
            |(?:{ipv4})
            """;

        return new Regex(
            pattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(500));
    }
}
