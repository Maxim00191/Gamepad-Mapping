#nullable enable

using System;

namespace GamepadMapperGUI.Utils.Text;

public static class UploadTextPunctuationCanonicalizer
{
    public static string Canonicalize(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        var any = false;
        for (var i = 0; i < s.Length; i++)
        {
            if (TryMap(s[i], out _))
            {
                any = true;
                break;
            }
        }

        if (!any)
            return s;

        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (TryMap(chars[i], out var mapped))
                chars[i] = mapped;
        }

        return new string(chars);
    }

    private static bool TryMap(char c, out char mapped)
    {
        mapped = c switch
        {
            '\uFF0C' => ',',
            '\u3002' => '.',
            '\uFF0E' => '.',
            '\uFF1B' => ';',
            _ => c
        };
        return mapped != c;
    }
}
