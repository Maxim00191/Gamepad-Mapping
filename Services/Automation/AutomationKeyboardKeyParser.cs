#nullable enable

using System.Windows.Input;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationKeyboardKeyParser
{
    public static bool TryParse(string? keyText, out Key key)
    {
        key = Key.None;
        if (string.IsNullOrWhiteSpace(keyText))
            return false;

        if (Enum.TryParse(keyText, true, out key))
            return true;

        if (keyText.Length != 1)
            return false;

        var c = char.ToUpperInvariant(keyText[0]);
        if (c is < 'A' or > 'Z')
            return false;

        key = Key.A + (c - 'A');
        return true;
    }
}
