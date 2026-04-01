namespace GamepadMapperGUI.Models;

public sealed class UiLanguageOption
{
    public UiLanguageOption(string cultureName, string displayName)
    {
        CultureName = cultureName;
        DisplayName = displayName;
    }

    public string CultureName { get; }

    public string DisplayName { get; }

    public override string ToString() => DisplayName;
}
