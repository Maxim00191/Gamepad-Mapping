#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    private static readonly Regex ObfuscatedIpv4 = BuildObfuscatedIpv4();

    public static bool ContainsBlockedContent(string? text)
    {
        var normalizedText = UploadLinkTextNormalizer.NormalizeForLinkDetection(text);
        if (normalizedText.Length == 0)
            return false;

        if (ContainsExplicitShortenerHost(normalizedText))
            return true;

        if (ContainsIpAddressToken(normalizedText))
            return true;

        if (normalizedText.Length > MaxCharsForFullRegexScan)
        {
            if (ContainsLikelyBlockedLinkLinear(normalizedText))
                return true;

            const int edge = 65_536;
            var head = normalizedText.AsSpan(0, edge);
            if (Combined.IsMatch(head))
                return true;

            var tail = normalizedText.AsSpan(normalizedText.Length - edge);
            return Combined.IsMatch(tail);
        }

        return Combined.IsMatch(normalizedText);
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

                if (IsHostStartBoundary(text, idx)
                    && IsHostTerminalAfterMatch(text, idx + host.Length))
                    return true;

                idx++;
            }
        }

        return false;
    }

    private static bool IsHostStartBoundary(string text, int indexOfHost)
    {
        if (indexOfHost <= 0)
            return true;

        return !IsAsciiLetter(text[indexOfHost - 1]);
    }

    private static bool IsAsciiLetter(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    private static bool IsHostTerminalAfterMatch(string text, int indexAfterHost)
    {
        if (indexAfterHost >= text.Length)
            return true;

        var c = text[indexAfterHost];
        return c is '/' or '\\' or ':' or '?' or '#' or '&' or '=' or '%'
            or '.' or ',' or ';' or '!' or ')' or ']' or '}' or '"' or '\'' or '>'
            || char.IsWhiteSpace(c);
    }

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

    private static Regex BuildObfuscatedIpv4()
    {
        const string seg = @"(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d|x{1,3})";
        const string pattern = $"^{seg}\\.{seg}\\.{seg}\\.{seg}$";
        return new Regex(
            pattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100));
    }

    private static bool ContainsIpAddressToken(string text)
    {
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (IsIpTokenDelimiter(text[i]))
            {
                if (start >= 0 && IsBlockedIpToken(text.AsSpan(start, i - start)))
                    return true;
                start = -1;
                continue;
            }

            if (start < 0)
                start = i;
        }

        return start >= 0 && IsBlockedIpToken(text.AsSpan(start));
    }

    private static bool IsIpTokenDelimiter(char c) =>
        char.IsWhiteSpace(c) || c is '<' or '>' or '"' or '\'' or '(' or ')' or '{' or '}' or ',' or ';' or '!' or '?' or '\\' or '/';

    private static bool IsBlockedIpToken(ReadOnlySpan<char> token)
    {
        token = TrimBoundaryPunctuation(token);
        if (token.Length < 3)
            return false;

        if (token[0] == '[' && token[^1] == ']')
            token = token.Slice(1, token.Length - 2);
        if (token.Length < 3)
            return false;

        var candidate = token.ToString();
        if (candidate.IndexOf(':') >= 0)
        {
            return IPAddress.TryParse(candidate, out var ipv6) && ipv6.AddressFamily == AddressFamily.InterNetworkV6;
        }

        if (candidate.IndexOf('.') >= 0)
        {
            var dotCount = CountChar(candidate, '.');
            if (dotCount != 3)
                return false;

            if (IPAddress.TryParse(candidate, out var ipv4) && ipv4.AddressFamily == AddressFamily.InterNetwork)
                return true;

            return ObfuscatedIpv4.IsMatch(candidate);
        }

        return false;
    }

    private static ReadOnlySpan<char> TrimBoundaryPunctuation(ReadOnlySpan<char> token)
    {
        var start = 0;
        var end = token.Length - 1;
        while (start <= end && IsTrimBoundary(token[start]))
            start++;
        while (end >= start && IsTrimBoundary(token[end]))
            end--;

        return start > end ? ReadOnlySpan<char>.Empty : token.Slice(start, end - start + 1);
    }

    private static bool IsTrimBoundary(char c) => c is '.' or ',' or ';' or '!' or '?' or ')' or ']' or '}' or '"' or '\'' or '(' or '[' or '{';

    private static int CountChar(string s, char value)
    {
        var count = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == value)
                count++;
        }

        return count;
    }
}
