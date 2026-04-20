using System.Collections.Generic;
using System.Globalization;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Utils;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GamepadMapperGUI.Models;

public sealed partial class RadialMenuItem : ObservableObject
{
    private string _actionId = string.Empty;
    private string? _icon;
    private string _label = string.Empty;
    private string _resolvedLabel = string.Empty;
    private Dictionary<string, string>? _labels;

    /// <summary>
    /// Reference to an Id in keyboardActions
    /// </summary>
    [JsonProperty("actionId")]
    public string ActionId
    {
        get => _actionId;
        set => SetProperty(ref _actionId, value);
    }

    /// <summary>
    /// Optional icon path or key
    /// </summary>
    [JsonProperty("icon", NullValueHandling = NullValueHandling.Ignore)]
    public string? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    /// <summary>Optional HUD line for this slot; falls back to the keyboard action description when empty after localization.</summary>
    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string Label
    {
        get => _label;
        set
        {
            if (!SetProperty(ref _label, value))
                return;
            ApplyResolvedLabelFromCatalog();
        }
    }

    /// <summary>HUD line for the current app language; canonical text remains in <see cref="Label"/>.</summary>
    [JsonIgnore]
    public string ResolvedLabel
    {
        get => _resolvedLabel;
        set => SetProperty(ref _resolvedLabel, value);
    }

    /// <summary>Optional per-culture HUD lines for this slot. Applied on template load; overrides <see cref="Label"/> when the UI culture matches.</summary>
    [JsonProperty("labels", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? Labels
    {
        get => _labels;
        set
        {
            if (!SetProperty(ref _labels, value))
                return;
            ApplyResolvedLabelFromCatalog();
        }
    }

    /// <summary>HUD line in the current UI language; the other language is the secondary field.</summary>
    [JsonIgnore]
    public string LabelPrimary
    {
        get => UiCultureDescriptionPair.ReadPrimary(Labels, Label, AppUiLocalization.EditorUiCulture());
        set
        {
            var ui = AppUiLocalization.EditorUiCulture();
            var secondary = UiCultureDescriptionPair.ReadSecondary(Labels, Label, ui);
            ApplyLabelPair(ui, value ?? string.Empty, secondary);
        }
    }

    /// <summary>Optional HUD line in the language that is not the current UI language.</summary>
    [JsonIgnore]
    public string LabelSecondary
    {
        get => UiCultureDescriptionPair.ReadSecondary(Labels, Label, AppUiLocalization.EditorUiCulture());
        set
        {
            var ui = AppUiLocalization.EditorUiCulture();
            var primary = UiCultureDescriptionPair.ReadPrimary(Labels, Label, ui);
            ApplyLabelPair(ui, primary, value ?? string.Empty);
        }
    }

    /// <summary>Raises <see cref="LabelPrimary"/> / <see cref="LabelSecondary"/> (e.g. after UI language change).</summary>
    public void NotifyEditorLabelFieldsChanged()
    {
        OnPropertyChanged(nameof(LabelPrimary));
        OnPropertyChanged(nameof(LabelSecondary));
    }

    private void ApplyLabelPair(CultureInfo ui, string primary, string secondary)
    {
        var d = Labels;
        var b = Label;
        UiCultureDescriptionPair.WritePair(ref d, ref b, ui, primary, secondary);

        var labelChanged = !string.Equals(_label, b, StringComparison.Ordinal);
        var mapChanged = !LocalizedCultureStringMap.ContentEquals(_labels, d);
        if (!labelChanged && !mapChanged)
            return;

        _label = b;
        _labels = d;
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Labels));
        ApplyResolvedLabelFromCatalog();
    }

    private void ApplyResolvedLabelFromCatalog()
    {
        if (AppUiLocalization.TryTranslationService() is { } ts)
            CatalogDescriptionLocalizer.ApplyRadialMenuItem(this, ts);
        else
            NotifyEditorLabelFieldsChanged();
    }
}
