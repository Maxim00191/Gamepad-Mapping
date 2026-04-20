#nullable enable

using System.Globalization;
using System.Windows;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Single entry point for WPF UI string lookup and editor culture (backed by <see cref="TranslationService"/> in application resources).
/// Reduces duplicated <c>Application.Current.Resources["Loc"]</c> and private <c>Loc()</c> helpers across profile editors.
/// </summary>
public static class AppUiLocalization
{
    /// <summary>Resource keys for prompts passed to <see cref="GamepadMapperGUI.Interfaces.Services.Input.IKeyboardCaptureService.BeginCapture"/>.</summary>
    public static class KeyboardCapturePromptKeys
    {
        public const string MappingOutput = "KeyboardCapture_Prompt_MappingOutput";
        public const string MappingHoldOutput = "KeyboardCapture_Prompt_MappingHoldOutput";
        public const string CatalogActionOutput = "KeyboardCapture_Prompt_CatalogActionOutput";
        public const string ItemCycleForward = "KeyboardCapture_Prompt_ItemCycleForward";
        public const string ItemCycleBackward = "KeyboardCapture_Prompt_ItemCycleBackward";
        public const string NewBindingOutput = "KeyboardCapture_Prompt_NewBindingOutput";
        public const string NewBindingHoldOutput = "KeyboardCapture_Prompt_NewBindingHoldOutput";
    }

    public static TranslationService? TryTranslationService() =>
        Application.Current?.Resources["Loc"] as TranslationService;

    /// <summary>Culture used for primary/secondary bilingual template fields (<see cref="UiCultureDescriptionPair"/>).</summary>
    public static CultureInfo EditorUiCulture() =>
        TryTranslationService()?.Culture ?? CultureInfo.CurrentUICulture;

    public static string GetString(string key)
    {
        if (TryTranslationService() is { } loc)
            return loc[key];
        return key;
    }

    /// <summary>Label for the optional bilingual description line (the language that is not the current UI language).</summary>
    public static string OptionalAlternateLanguageDescriptionCaption()
    {
        if (TryTranslationService() is not { } ts)
            return string.Empty;

        return UiCultureDescriptionPair.IsChinesePrimaryUi(ts.Culture)
            ? ts["CatalogDescriptionOptionalEnglish"]
            : ts["CatalogDescriptionOptionalChinese"];
    }
}
