using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gamepad_Mapping.ViewModels;

public partial class RadialMenuHudViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = string.Empty;

    public ObservableCollection<RadialMenuItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private int selectedIndex = -1;

    [ObservableProperty]
    private double rotationAngle = 0;
}

public partial class RadialMenuItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string actionId = string.Empty;

    [ObservableProperty]
    private string primaryCaption = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecondaryCaption))]
    private string? secondaryCaption;

    public bool HasSecondaryCaption => !string.IsNullOrEmpty(SecondaryCaption);

    [ObservableProperty]
    private string? icon;

    [ObservableProperty]
    private bool isSelected;

    /// <summary>0 .. SegmentCount-1, clockwise from top — drives layout; must match mapping engine sectors.</summary>
    [ObservableProperty]
    private int segmentIndex;

    [ObservableProperty]
    private int segmentCount;
}
