#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationColorSelectionParser
{
    private const int DefaultHueTolerance = 10;
    private const int DefaultSaturationTolerance = 48;
    private const int DefaultValueTolerance = 48;

    public static AutomationColorDetectionOptions ApplyTargetHex(
        AutomationColorDetectionOptions baseline,
        string? targetHex)
    {
        if (!TryParseHexRgb(targetHex, out var r, out var g, out var b))
            return baseline;

        var (h, s, v) = RgbToHsv(r, g, b);
        return new AutomationColorDetectionOptions(
            WrapHueFloor(h - DefaultHueTolerance),
            WrapHueCeil(h + DefaultHueTolerance),
            ClampByte(s - DefaultSaturationTolerance),
            ClampByte(s + DefaultSaturationTolerance),
            ClampByte(v - DefaultValueTolerance),
            ClampByte(v + DefaultValueTolerance),
            baseline.MinimumAreaPx);
    }

    private static bool TryParseHexRgb(string? raw, out int r, out int g, out int b)
    {
        r = 0;
        g = 0;
        b = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim();
        if (text.StartsWith('#'))
            text = text[1..];

        if (text.Length != 6)
            return false;

        if (!int.TryParse(text[..2], System.Globalization.NumberStyles.HexNumber, null, out r))
            return false;
        if (!int.TryParse(text.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g))
            return false;
        if (!int.TryParse(text.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b))
            return false;

        return true;
    }

    private static (int H, int S, int V) RgbToHsv(int r, int g, int b)
    {
        var rf = r / 255d;
        var gf = g / 255d;
        var bf = b / 255d;

        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        double hue;
        if (delta <= 0.000001d)
        {
            hue = 0;
        }
        else if (Math.Abs(max - rf) < 0.000001d)
        {
            hue = 60d * (((gf - bf) / delta) % 6d);
        }
        else if (Math.Abs(max - gf) < 0.000001d)
        {
            hue = 60d * (((bf - rf) / delta) + 2d);
        }
        else
        {
            hue = 60d * (((rf - gf) / delta) + 4d);
        }

        if (hue < 0)
            hue += 360d;

        var saturation = max <= 0.000001d ? 0 : delta / max;
        var hOpenCv = (int)Math.Round(hue / 2d);
        var sOpenCv = (int)Math.Round(saturation * 255d);
        var vOpenCv = (int)Math.Round(max * 255d);
        return (Math.Clamp(hOpenCv, 0, 179), Math.Clamp(sOpenCv, 0, 255), Math.Clamp(vOpenCv, 0, 255));
    }

    private static int WrapHueFloor(int value)
    {
        while (value < 0)
            value += 180;
        return Math.Clamp(value, 0, 179);
    }

    private static int WrapHueCeil(int value)
    {
        while (value >= 180)
            value -= 180;
        return Math.Clamp(value, 0, 179);
    }

    private static int ClampByte(int value) => Math.Clamp(value, 0, 255);
}
