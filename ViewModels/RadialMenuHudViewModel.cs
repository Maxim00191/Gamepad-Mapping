using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
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

    private SolidColorBrush _sectorNormalFillBrush = null!;
    private SolidColorBrush _sectorSelectedFillBrush = null!;
    private SolidColorBrush _sectorStrokeBrush = null!;
    private SolidColorBrush _sectorSelectedStrokeBrush = null!;
    private SolidColorBrush _titlePlateFillBrush = null!;
    private SolidColorBrush _titlePlateStrokeBrush = null!;

    public SolidColorBrush SectorNormalFillBrush => _sectorNormalFillBrush;
    public SolidColorBrush SectorSelectedFillBrush => _sectorSelectedFillBrush;
    public SolidColorBrush SectorStrokeBrush => _sectorStrokeBrush;
    public SolidColorBrush SectorSelectedStrokeBrush => _sectorSelectedStrokeBrush;
    public SolidColorBrush TitlePlateFillBrush => _titlePlateFillBrush;
    public SolidColorBrush TitlePlateStrokeBrush => _titlePlateStrokeBrush;

    public RadialMenuHudViewModel() => ApplyHudBackingAlpha(96);

    /// <summary>Applies combo HUD panel alpha from app settings (same slider as the on-screen combo guide).</summary>
    public void ApplyHudBackingAlpha(int comboHudPanelAlpha)
    {
        var a = (byte)Math.Clamp(comboHudPanelAlpha, 24, 220);
        var accent = ResolveAccentRgb();

        SetBrush(ref _sectorNormalFillBrush, 0x1A, 0x1A, 0x1E, a, nameof(SectorNormalFillBrush));
        SetBrush(ref _sectorSelectedFillBrush, accent.R, accent.G, accent.B, a, nameof(SectorSelectedFillBrush));
        SetBrush(ref _sectorStrokeBrush, 0xFF, 0xFF, 0xFF, (byte)Math.Min(255, a + 50), nameof(SectorStrokeBrush));
        SetBrush(ref _sectorSelectedStrokeBrush, accent.R, accent.G, accent.B, (byte)Math.Min(255, a + 60), nameof(SectorSelectedStrokeBrush));
        SetBrush(ref _titlePlateFillBrush, 0x28, 0x28, 0x30, a, nameof(TitlePlateFillBrush));
        SetBrush(ref _titlePlateStrokeBrush, 0xFF, 0xFF, 0xFF, (byte)Math.Min(255, a + 50), nameof(TitlePlateStrokeBrush));
    }

    private void SetBrush(ref SolidColorBrush field, byte r, byte g, byte b, byte alpha, string propertyName)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
        brush.Freeze();
        field = brush;
        OnPropertyChanged(propertyName);
    }

    private static (byte R, byte G, byte B) ResolveAccentRgb()
    {
        if (Application.Current?.TryFindResource("AppAccentBrush") is SolidColorBrush sb)
            return (sb.Color.R, sb.Color.G, sb.Color.B);
        return (70, 90, 130);
    }
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
