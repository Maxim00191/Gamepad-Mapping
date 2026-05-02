namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationDataValue(AutomationPortType PortType, object? Value)
{
    public static readonly AutomationDataValue Empty = new(AutomationPortType.Any, null);

    public bool TryGetNumber(out double value)
    {
        value = 0;
        if (Value is null)
            return false;

        return Value switch
        {
            double d => SetNumber(d, out value),
            float f => SetNumber(f, out value),
            int i => SetNumber(i, out value),
            long l => SetNumber(l, out value),
            decimal m => SetNumber((double)m, out value),
            _ => double.TryParse(Value.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value)
        };
    }

    public bool TryGetBoolean(out bool value)
    {
        value = false;
        if (Value is null)
            return false;

        if (Value is bool b)
        {
            value = b;
            return true;
        }

        return bool.TryParse(Value.ToString(), out value);
    }

    public string GetStringOrEmpty() => Value?.ToString() ?? string.Empty;

    private static bool SetNumber(double candidate, out double output)
    {
        output = candidate;
        return true;
    }
}
