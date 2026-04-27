using System;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.ViewModels;

public partial class CommunityTemplateUploadBundleRowViewModel : ObservableObject
{
    private readonly Action _onIncludedChanged;

    public CommunityTemplateUploadBundleRowViewModel(
        string storageKey,
        GameProfileTemplate template,
        Action onIncludedChanged)
    {
        StorageKey = storageKey;
        Template = template;
        _onIncludedChanged = onIncludedChanged;

        var pid = (template.ProfileId ?? string.Empty).Trim();
        var baseline = (template.DisplayName ?? string.Empty).Trim();
        if (baseline.Length == 0)
            baseline = pid.Length > 0 ? pid : storageKey;

        TitleLine = CommunityTemplateDisplayLabels.ResolveGameProfileTemplateTitle(
            template,
            baseline,
            AppUiLocalization.TryTranslationService());
        SubtitleLine = storageKey;
    }

    public string StorageKey { get; }

    public string TitleLine { get; }

    public string SubtitleLine { get; }

    public GameProfileTemplate Template { get; }

    [ObservableProperty]
    private bool _isIncluded = true;

    partial void OnIsIncludedChanged(bool value) => _onIncludedChanged();
}
