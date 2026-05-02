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
        Color titleBarBackground,
        Color titleBarForeground,
        Color titleBarBorder,
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
        Color hudOverlayBorderBase,
        Color softWarningBorder,
        Color unsavedChangesIndicator,
        Color semanticWarningForeground,
        Color semanticErrorForeground,
        Color tooltipBackground,
        Color tooltipBorder)
    {
        Background = background;
        Surface = surface;
        Border = border;
        TitleBarBackground = titleBarBackground;
        TitleBarForeground = titleBarForeground;
        TitleBarBorder = titleBarBorder;
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
        SoftWarningBorder = softWarningBorder;
        UnsavedChangesIndicator = unsavedChangesIndicator;
        SemanticWarningForeground = semanticWarningForeground;
        SemanticErrorForeground = semanticErrorForeground;
        TooltipBackground = tooltipBackground;
        TooltipBorder = tooltipBorder;
    }

    public Color Background { get; }
    public Color Surface { get; }
    public Color Border { get; }
    public Color TitleBarBackground { get; }
    public Color TitleBarForeground { get; }
    public Color TitleBarBorder { get; }
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

    /// <summary>Breathing border for soft-warning button highlight (must read on control surfaces).</summary>
    public Color SoftWarningBorder { get; }

    /// <summary>Workspace unsaved indicator glyph and label.</summary>
    public Color UnsavedChangesIndicator { get; }

    /// <summary>Warning semantics on tinted panels (e.g. duplicate bindings, validation warnings).</summary>
    public Color SemanticWarningForeground { get; }

    /// <summary>Error semantics on tinted panels (e.g. validation errors).</summary>
    public Color SemanticErrorForeground { get; }

    /// <summary>Tooltip chrome; slightly separated from <see cref="Surface"/> for legibility.</summary>
    public Color TooltipBackground { get; }

    /// <summary>Tooltip outline (typically aligned with <see cref="Border"/>).</summary>
    public Color TooltipBorder { get; }

    /// <summary>Warm muted light chrome: lower luminance surfaces, soft warm grays, restrained gold accents.</summary>
    public static AppChromeTheme Light { get; } = new(
        background: Color.FromRgb(0xF2, 0xF0, 0xEC),
        surface: Color.FromRgb(0xF7, 0xF5, 0xF0),
        border: Color.FromRgb(0x50, 0x4C, 0x46),
        titleBarBackground: Color.FromRgb(0xF2, 0xF0, 0xEC),
        titleBarForeground: Color.FromRgb(0x24, 0x22, 0x1E),
        titleBarBorder: Color.FromRgb(0xA8, 0xA2, 0x98),
        text: Color.FromRgb(0x24, 0x22, 0x1E),
        secondaryText: Color.FromRgb(0x73, 0x6E, 0x66),
        accentText: Color.FromRgb(0x8A, 0x60, 0x18),
        controlSurface: Color.FromRgb(0xF7, 0xF5, 0xF0),
        controlSurfaceAlt: Color.FromRgb(0xEC, 0xE8, 0xE0),
        controlHover: Color.FromRgb(0xE2, 0xDD, 0xD4),
        controlPressed: Color.FromRgb(0xD8, 0xD2, 0xC8),
        accent: Color.FromRgb(0x2A, 0x4A, 0x7A),
        accentHover: Color.FromRgb(0x22, 0x3C, 0x66),
        selection: Color.FromRgb(0xDD, 0xD4, 0xC4),
        selectionText: Color.FromRgb(0x24, 0x22, 0x1E),
        scrollTrack: Color.FromRgb(0xE2, 0xDD, 0xD4),
        scrollThumb: Color.FromRgb(0xA8, 0xA2, 0x98),
        scrollThumbHover: Color.FromRgb(0x8A, 0x84, 0x7A),
        separator: Color.FromRgb(0xD8, 0xD2, 0xC8),
        gridSplitter: Color.FromRgb(0xBC, 0xB5, 0xAA),
        gridSplitterHover: Color.FromRgb(0xB8, 0x94, 0x28),
        hudTitle: Color.FromRgb(0x24, 0x22, 0x20),
        hudDetail: Color.FromRgb(0x5F, 0x5A, 0x54),
        toastBackground: Color.FromArgb(210, 0xF7, 0xF5, 0xF0),
        hudOverlayPanelBase: Color.FromRgb(0xF0, 0xED, 0xE8),
        hudOverlayBorderBase: Color.FromRgb(0x58, 0x52, 0x4A),
        // Darker amber than control surfaces so the animated soft-warning rim stays visible on warm light UI.
        softWarningBorder: Color.FromRgb(0xB3, 0x5F, 0x00),
        unsavedChangesIndicator: Color.FromRgb(0xB3, 0x5F, 0x00),
        semanticWarningForeground: Color.FromRgb(0x9A, 0x34, 0x12),
        semanticErrorForeground: Color.FromRgb(0xB9, 0x1C, 0x1C),
        tooltipBackground: Color.FromRgb(0xFD, 0xFB, 0xF7),
        tooltipBorder: Color.FromRgb(0x50, 0x4C, 0x46));

    public static AppChromeTheme Dark { get; } = new(
        background: Color.FromRgb(24, 24, 27),
        surface: Color.FromRgb(35, 35, 40),
        border: Color.FromRgb(95, 95, 105),
        titleBarBackground: Color.FromRgb(24, 24, 27),
        titleBarForeground: Color.FromRgb(238, 238, 242),
        titleBarBorder: Color.FromRgb(72, 72, 84),
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
        hudOverlayBorderBase: Color.FromRgb(0, 0, 0),
        softWarningBorder: Color.FromRgb(0xE8, 0x9B, 0x10),
        unsavedChangesIndicator: Color.FromRgb(0xC9, 0xA0, 0x00),
        semanticWarningForeground: Color.FromRgb(0xFB, 0x92, 0x3C),
        semanticErrorForeground: Color.FromRgb(0xF8, 0x71, 0x71),
        tooltipBackground: Color.FromRgb(0x36, 0x36, 0x3C),
        tooltipBorder: Color.FromRgb(0x68, 0x68, 0x74));

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
        resources["AppSoftWarningBorderBrush"] = Brush(SoftWarningBorder);
        resources["AppUnsavedChangesIndicatorBrush"] = Brush(UnsavedChangesIndicator);
        resources["AppSemanticWarningForegroundBrush"] = Brush(SemanticWarningForeground);
        resources["AppSemanticErrorForegroundBrush"] = Brush(SemanticErrorForeground);
        resources["AppTooltipBackgroundBrush"] = Brush(TooltipBackground);
        resources["AppTooltipBorderBrush"] = Brush(TooltipBorder);

        resources[SystemColors.MenuBarColorKey] = Surface;
        resources[SystemColors.MenuColorKey] = ControlSurface;
        resources[SystemColors.MenuTextColorKey] = Text;
        resources[SystemColors.MenuBarBrushKey] = Brush(Surface);
        resources[SystemColors.MenuBrushKey] = Brush(ControlSurface);
        resources[SystemColors.MenuTextBrushKey] = Brush(Text);
        resources[SystemColors.MenuHighlightBrushKey] = Brush(ControlHover);
        resources[SystemColors.HighlightBrushKey] = Brush(Selection);
        resources[SystemColors.HighlightTextBrushKey] = Brush(SelectionText);
        resources[SystemColors.GrayTextBrushKey] = Brush(SecondaryText);
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
