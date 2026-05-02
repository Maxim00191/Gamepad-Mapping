#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationOcrPhraseMatchOptions(
    string PhrasesMultiline,
    bool CaseSensitive = false,
    int MaxLongEdgePx = 0)
{
    public static AutomationOcrPhraseMatchOptions Default { get; } = new("", false, 0);
}
