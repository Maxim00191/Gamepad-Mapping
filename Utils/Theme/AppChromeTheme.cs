#nullable enable

using System;
using System.Windows;
using System.Windows.Media;

namespace Gamepad_Mapping.Utils.Theme;

/// <summary>
/// Application chrome colors for light and dark system themes. Centralizes palette values for resource
/// brushes and HUD overlay tinting so they stay aligned.
/// </summary>
internal sealed class AppChromeTheme
{
    private AppChromeTheme(
        Color background,
        Color surface,
        Color border,
        Color text,
        Color secondaryText,
        Color accentText,
        Color controlSurface,
        Color controlSurfaceAlt,
        Color controlHover,
        Color controlPressed,
        Color accent,
        Color accentHover,
        Color selection,
        Color selectionText,
        Color scrollTrack,
        Color scrollThumb,
        Color scrollThumbHover,
        Color separator,
        Color gridSplitter,
        Color gridSplitterHover,
        Color hudTitle,
        Color hudDetail,
        Color toastBackground,
        Color hudOverlayPanelBase,
        Color hudOverlayBorderBase)
    {
        Background = background;
        Surface = surface;
        Border = border;
        Text = text;
        SecondaryText = secondaryText;
        AccentText = accentText;
        ControlSurface = controlSurface;
        ControlSurfaceAlt = controlSurfaceAlt;
        ControlHover = controlHover;
        ControlPressed = controlPressed;
        Accent = accent;
        AccentHover = accentHover;
        Selection = selection;
        SelectionText = selectionText;
        ScrollTrack = scrollTrack;
        ScrollThumb = scrollThumb;
        ScrollThumbHover = scrollThumbHover;
        Separator = separator;
        GridSplitter = gridSplitter;
        GridSplitterHover = gridSplitterHover;
        HudTitle = hudTitle;
        HudDetail = hudDetail;
        ToastBackground = toastBackground;
        HudOverlayPanelBase = hudOverlayPanelBase;
        HudOverlayBorderBase = hudOverlayBorderBase;
    }

    public Color Background { get; }
    public Color Surface { get; }
    public Color Border { get; }
    public Color Text { get; }
    public Color SecondaryText { get; }
    public Color AccentText { get; }
    public Color ControlSurface { get; }
    public Color ControlSurfaceAlt { get; }
    public Color ControlHover { get; }
    public Color ControlPressed { get; }
    public Color Accent { get; }
    public Color AccentHover { get; }
    public Color Selection { get; }
    public Color SelectionText { get; }
    public Color ScrollTrack { get; }
    public Color ScrollThumb { get; }
    public Color ScrollThumbHover { get; }
    public Color Separator { get; }
    public Color GridSplitter { get; }
    public Color GridSplitterHover { get; }
    public Color HudTitle { get; }
    public Color HudDetail { get; }
    public Color ToastBackground { get; }
    public Color HudOverlayPanelBase { get; }
    public Color HudOverlayBorderBase { get; }

    /// <summary>Warm muted light chrome: lower luminance surfaces, soft warm grays, restrained gold accents.</summary>
    public static AppChromeTheme Light { get; } = new(
        background: Color.FromRgb(0xE6, 0xE3, 0xDC),
        surface: Color.FromRgb(0xED, 0xEA, 0xE3),
        border: Color.FromRgb(0x50, 0x4C, 0x46),
        text: Color.FromRgb(0x24, 0x22, 0x1E),
        secondaryText: Color.FromRgb(0x73, 0x6E, 0x66),
        accentText: Color.FromRgb(0x8A, 0x60, 0x18),
        controlSurface: Color.FromRgb(0xED, 0xEA, 0xE3),
        controlSurfaceAlt: Color.FromRgb(0xE0, 0xDB, 0xD2),
        controlHover: Color.FromRgb(0xD5, 0xD0, 0xC6),
        controlPressed: Color.FromRgb(0xC9, 0xC3, 0xB8),
        accent: Color.FromRgb(0x2A, 0x4A, 0x7A),
        accentHover: Color.FromRgb(0x22, 0x3C, 0x66),
        selection: Color.FromRgb(0xDD, 0xD4, 0xC4),
        selectionText: Color.FromRgb(0x24, 0x22, 0x1E),
        scrollTrack: Color.FromRgb(0xD4, 0xD1, 0xC9),
        scrollThumb: Color.FromRgb(0xA8, 0xA2, 0x98),
        scrollThumbHover: Color.FromRgb(0x8A, 0x84, 0x7A),
        separator: Color.FromRgb(0xC9, 0xC3, 0xB8),
        gridSplitter: Color.FromRgb(0xBC, 0xB5, 0xAA),
        gridSplitterHover: Color.FromRgb(0xB8, 0x94, 0x28),
        hudTitle: Color.FromRgb(0x24, 0x22, 0x20),
        hudDetail: Color.FromRgb(0x5F, 0x5A, 0x54),
        toastBackground: Color.FromArgb(210, 0xED, 0xEA, 0xE3),
        hudOverlayPanelBase: Color.FromRgb(0xE8, 0xE4, 0xDC),
        hudOverlayBorderBase: Color.FromRgb(0x58, 0x52, 0x4A));

    public static AppChromeTheme Dark { get; } = new(
        background: Color.FromRgb(24, 24, 27),
        surface: Color.FromRgb(35, 35, 40),
        border: Color.FromRgb(95, 95, 105),
        text: Color.FromRgb(238, 238, 242),
        secondaryText: Color.FromRgb(185, 185, 195),
        accentText: Color.FromRgb(255, 135, 95),
        controlSurface: Color.FromRgb(43, 43, 49),
        controlSurfaceAlt: Color.FromRgb(50, 50, 58),
        controlHover: Color.FromRgb(62, 62, 72),
        controlPressed: Color.FromRgb(78, 78, 91),
        accent: Color.FromRgb(50, 100, 198),
        accentHover: Color.FromRgb(72, 120, 225),
        selection: Color.FromRgb(62, 83, 122),
        selectionText: Color.FromRgb(245, 247, 250),
        scrollTrack: Color.FromRgb(54, 54, 63),
        scrollThumb: Color.FromRgb(104, 104, 116),
        scrollThumbHover: Color.FromRgb(132, 132, 146),
        separator: Color.FromRgb(62, 62, 72),
        gridSplitter: Color.FromRgb(72, 72, 84),
        gridSplitterHover: Color.FromRgb(50, 100, 198),
        hudTitle: Color.FromRgb(245, 246, 250),
        hudDetail: Color.FromRgb(205, 210, 220),
        toastBackground: Color.FromArgb(210, 42, 42, 48),
        hudOverlayPanelBase: Color.FromRgb(28, 28, 30),
        hudOverlayBorderBase: Color.FromRgb(0, 0, 0));

    public void ApplyTo(ResourceDictionary resources)
    {
        resources["AppBackgroundBrush"] = Brush(Background);
        resources["AppSurfaceBrush"] = Brush(Surface);
        resources["AppBorderBrush"] = Brush(Border);
        resources["AppTextBrush"] = Brush(Text);
        resources["AppSecondaryTextBrush"] = Brush(SecondaryText);
        resources["AppAccentTextBrush"] = Brush(AccentText);
        resources["AppControlSurfaceBrush"] = Brush(ControlSurface);
        resources["AppControlSurfaceAltBrush"] = Brush(ControlSurfaceAlt);
        resources["AppControlHoverBrush"] = Brush(ControlHover);
        resources["AppControlPressedBrush"] = Brush(ControlPressed);
        resources["AppAccentBrush"] = Brush(Accent);
        resources["AppAccentHoverBrush"] = Brush(AccentHover);
        resources["AppSelectionBrush"] = Brush(Selection);
        resources["AppSelectionTextBrush"] = Brush(SelectionText);
        resources["AppScrollTrackBrush"] = Brush(ScrollTrack);
        resources["AppScrollThumbBrush"] = Brush(ScrollThumb);
        resources["AppScrollThumbHoverBrush"] = Brush(ScrollThumbHover);
        resources["AppSeparatorBrush"] = Brush(Separator);
        resources["AppGridSplitterBrush"] = Brush(GridSplitter);
        resources["AppGridSplitterHoverBrush"] = Brush(GridSplitterHover);
        resources["AppHudTitleBrush"] = Brush(HudTitle);
        resources["AppHudDetailBrush"] = Brush(HudDetail);
        resources["AppToastBackgroundBrush"] = Brush(ToastBackground);
    }

    public static void ApplyHudChrome(
        bool usesLightTheme,
        AppChromeTheme theme,
        byte panelAlpha,
        out Color panelColor,
        out Color borderColor)
    {
        panelAlpha = (byte)Math.Clamp((int)panelAlpha, 24, 220);
        panelColor = Color.FromArgb(panelAlpha, theme.HudOverlayPanelBase.R, theme.HudOverlayPanelBase.G, theme.HudOverlayPanelBase.B);
        var borderA = usesLightTheme
            ? (byte)Math.Clamp(panelAlpha / 3 + 24, 44, 100)
            : (byte)Math.Clamp(panelAlpha / 2 + 28, 32, 100);
        borderColor = Color.FromArgb(borderA, theme.HudOverlayBorderBase.R, theme.HudOverlayBorderBase.G, theme.HudOverlayBorderBase.B);
    }

    private static SolidColorBrush Brush(Color color) => new(color);
}
