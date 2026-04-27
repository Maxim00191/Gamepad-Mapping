using System.Text.Json.Nodes;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationNodePropertyReader
{
    public static void WriteString(JsonObject props, string key, string value) => props[key] = value;

    public static void WriteDouble(JsonObject props, string key, double value) => props[key] = value;

    public static void WriteInt(JsonObject props, string key, int value) => props[key] = value;

    public static void WriteBool(JsonObject props, string key, bool value) => props[key] = value;

    public static string ReadString(JsonObject? props, string key)
    {
        if (props is null || !props.TryGetPropertyValue(key, out var n) || n is null)
            return "";

        if (n is JsonValue jv && jv.TryGetValue<string>(out var s))
            return s;

        return n.ToString();
    }

    public static double ReadDouble(JsonObject? props, string key, double defaultValue)
    {
        if (props is null || !props.TryGetPropertyValue(key, out var n) || n is null)
            return defaultValue;

        try
        {
            return double.Parse(n.ToString(), System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return defaultValue;
        }
    }

    public static bool ReadBool(JsonObject? props, string key)
    {
        if (props is null || !props.TryGetPropertyValue(key, out var n) || n is null)
            return false;

        return bool.TryParse(n.ToString(), out var b) && b;
    }

    public static int ReadInt(JsonObject? props, string key, int defaultValue)
    {
        if (props is null || !props.TryGetPropertyValue(key, out var n) || n is null)
            return defaultValue;

        return int.TryParse(n.ToString(), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v
            : defaultValue;
    }

    public static bool TryReadRoiCapture(JsonObject? props, out AutomationPhysicalRect roi)
    {
        roi = default;
        if (props is null || !props.TryGetPropertyValue(AutomationNodePropertyKeys.CaptureRoi, out var n) ||
            n is not JsonObject o)
            return false;

        var x = ReadIntFromObject(o, "x");
        var y = ReadIntFromObject(o, "y");
        var w = ReadIntFromObject(o, "width");
        var h = ReadIntFromObject(o, "height");
        roi = new AutomationPhysicalRect(x, y, w, h);
        return true;
    }

    private static int ReadIntFromObject(JsonObject o, string key)
    {
        if (!o.TryGetPropertyValue(key, out var n) || n is null)
            return 0;

        return int.TryParse(n.ToString(), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
    }
}
