using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using Gamepad_Mapping;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;

namespace Gamepad_Mapping.ViewModels;

public record UiLanguageOption(string CultureName, string DisplayName);

public record UiThemeOption(string Key, string DisplayName);

/// <summary>
/// Orchestrates application settings, persistence, and UI localization.
/// Merges AppSettings management with TranslationService and Culture logic.
/// </summary>
public partial class SettingsOrchestrator : ObservableObject
{
    private static readonly UiLanguageOption[] SupportedUiLanguages =
    [
        new("zh-CN", "简体中文"),
        new("en-US", "English")
    ];

    private readonly AppSettings _appSettings;
    private readonly ISettingsService _settingsService;
    private bool _isInitializingUiLanguageSelection;
    private bool _isInitializingUiThemeSelection;

    [ObservableProperty]
    private ObservableCollection<UiLanguageOption> _availableUiLanguages;

    [ObservableProperty]
    private UiLanguageOption? _selectedUiLanguage;

    [ObservableProperty]
    private ObservableCollection<UiThemeOption> _availableUiThemes;

    [ObservableProperty]
    private UiThemeOption? _selectedUiTheme;

    public AppSettings Settings => _appSettings;

    public SettingsOrchestrator(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _appSettings = _settingsService.LoadSettings();
        
        AvailableUiLanguages = new ObservableCollection<UiLanguageOption>(SupportedUiLanguages);

        AvailableUiThemes = new ObservableCollection<UiThemeOption>(
        [
            new(UiThemeMode.FollowSystem, Localize("UiThemeFollowSystem")),
            new(UiThemeMode.Light, Localize("UiThemeLight")),
            new(UiThemeMode.Dark, Localize("UiThemeDark"))
        ]);

        _isInitializingUiThemeSelection = true;
        var themeKey = UiThemeMode.Normalize(_appSettings.UiTheme);
        SelectedUiTheme =
            AvailableUiThemes.FirstOrDefault(x => string.Equals(x.Key, themeKey, StringComparison.Ordinal))
            ?? AvailableUiThemes[0];
        _isInitializingUiThemeSelection = false;

        _isInitializingUiLanguageSelection = true;
        SelectedUiLanguage = 
            AvailableUiLanguages.FirstOrDefault(x => string.Equals(x.CultureName, _appSettings.UiCulture, StringComparison.OrdinalIgnoreCase))
            ?? AvailableUiLanguages.FirstOrDefault(x => string.Equals(x.CultureName, "zh-CN", StringComparison.OrdinalIgnoreCase))
            ?? AvailableUiLanguages.FirstOrDefault();
        _isInitializingUiLanguageSelection = false;

        if (SelectedUiLanguage != null)
        {
            ApplyUiLanguage(SelectedUiLanguage.CultureName, persist: false);
        }
    }

    partial void OnSelectedUiLanguageChanged(UiLanguageOption? value)
    {
        if (value == null) return;
        ApplyUiLanguage(value.CultureName, persist: !_isInitializingUiLanguageSelection);
    }

    partial void OnSelectedUiThemeChanged(UiThemeOption? value)
    {
        if (value == null) return;
        var normalized = UiThemeMode.Normalize(value.Key);
        if (!_isInitializingUiThemeSelection)
        {
            if (!string.Equals(_appSettings.UiTheme, normalized, StringComparison.Ordinal))
            {
                _appSettings.UiTheme = normalized;
                SaveSettings();
            }

            App.RequestApplyChromeTheme();
        }
    }

    public void ApplyUiLanguage(string cultureName, bool persist)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo("zh-CN");
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        
        if (Application.Current?.Resources["Loc"] is TranslationService translationService)
        {
            translationService.Culture = culture;
        }

        if (persist && !string.Equals(_appSettings.UiCulture, culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            _appSettings.UiCulture = culture.Name;
            SaveSettings();
        }

        RefreshLocalizedUiThemeOptions();
    }

    /// <summary>
    /// Rebuilds localized display names for theme choices (Follow system / Light / Dark) after UI language changes.
    /// </summary>
    public void RefreshLocalizedUiThemeOptions()
    {
        var themeKey = UiThemeMode.Normalize(_appSettings.UiTheme);
        _isInitializingUiThemeSelection = true;
        try
        {
            AvailableUiThemes.Clear();
            AvailableUiThemes.Add(new UiThemeOption(UiThemeMode.FollowSystem, Localize("UiThemeFollowSystem")));
            AvailableUiThemes.Add(new UiThemeOption(UiThemeMode.Light, Localize("UiThemeLight")));
            AvailableUiThemes.Add(new UiThemeOption(UiThemeMode.Dark, Localize("UiThemeDark")));
            SelectedUiTheme =
                AvailableUiThemes.FirstOrDefault(x => string.Equals(x.Key, themeKey, StringComparison.Ordinal))
                ?? AvailableUiThemes[0];
        }
        finally
        {
            _isInitializingUiThemeSelection = false;
        }

        OnPropertyChanged(nameof(AvailableUiThemes));
        OnPropertyChanged(nameof(SelectedUiTheme));
    }

    public void SaveSettings()
    {
        _settingsService.SaveSettings(_appSettings);
    }

    public string Localize(string key) => AppUiLocalization.GetString(key);

    public string FormatUpdateSuccessMessage(string releaseTag)
    {
        if (Application.Current?.Resources["Loc"] is not TranslationService loc)
            return releaseTag;

        return string.Format(loc["UpdateSuccessToastMessage"], releaseTag);
    }

    public Func<string>? GetComboHudGateMessageFactory()
    {
        if (Application.Current?.Resources["Loc"] is TranslationService loc)
            return () => loc["ComboHudGateHint"];
        return null;
    }
}


