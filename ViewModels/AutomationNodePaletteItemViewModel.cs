#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;

namespace Gamepad_Mapping.ViewModels;

public sealed partial class AutomationNodePaletteItemViewModel : ObservableObject
{
    public required string NodeTypeId { get; init; }

    public required string CategoryTitle { get; init; }

    [ObservableProperty]
    private string _displayTitle = "";

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private string _glyph = "";

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isRecent;
}
