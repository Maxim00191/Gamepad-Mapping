using System;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;

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

        var dn = (template.DisplayName ?? string.Empty).Trim();
        var pid = (template.ProfileId ?? string.Empty).Trim();
        TitleLine = dn.Length > 0 ? dn : (pid.Length > 0 ? pid : storageKey);
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
