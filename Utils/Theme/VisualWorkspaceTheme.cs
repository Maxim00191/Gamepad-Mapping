#nullable enable

using System.Windows;
using System.Windows.Media;

namespace Gamepad_Mapping.Utils.Theme;

internal static class VisualWorkspaceTheme
{
    public static void Apply(ResourceDictionary resources, bool light)
    {
        if (light)
        {
            resources["VisualWorkspacePanelBackgroundBrush"] = Brush(Color.FromRgb(0xD4, 0xCE, 0xC4));
            resources["VisualWorkspacePanelBorderBrush"] = Brush(Color.FromRgb(0x8F, 0x88, 0x7E));
            resources["VisualWorkspaceListItemBackgroundBrush"] = Brush(Color.FromRgb(0xDF, 0xD9, 0xD0));
            resources["VisualWorkspaceListItemSelectedBackgroundBrush"] = Brush(Color.FromRgb(0xB8, 0xC8, 0xE0));
            resources["VisualWorkspaceListAccentBrush"] = Brush(Color.FromRgb(0x2A, 0x4A, 0x7A));
            resources["VisualWorkspaceListItemHoverBorderBrush"] = Brush(Color.FromRgb(0x7A, 0x73, 0x69));
            resources["VisualWorkspaceControllerBackgroundBrush"] = Brush(Color.FromRgb(0xC9, 0xC2, 0xB6));
            resources["VisualWorkspaceHintBarBackgroundBrush"] = Brush(Color.FromArgb(0xCC, 0xBE, 0xB6, 0xA8));
            resources["VisualWorkspaceHintBarBorderBrush"] = Brush(Color.FromRgb(0x8F, 0x88, 0x7E));
            resources["VisualWorkspaceHintBarPrimaryForegroundBrush"] = Brush(Color.FromRgb(0x24, 0x22, 0x1E));
            resources["VisualWorkspaceHintBarSecondaryForegroundBrush"] = Brush(Color.FromRgb(0x73, 0x6E, 0x66));
            resources["VisualWorkspaceOverlayLeaderMutedStrokeBrush"] = Brush(Color.FromArgb(0x66, 0x30, 0x2C, 0x26));
            resources["VisualWorkspaceOverlayLabelDotBrush"] = Brush(Color.FromArgb(0x44, 0x30, 0x2C, 0x26));
            resources["VisualWorkspacePathHoverFillBrush"] = Brush(Color.FromArgb(0x28, 0, 0, 0));
            resources["VisualWorkspacePathSelectedFillBrush"] = Brush(Color.FromArgb(0x45, 0, 0, 0));
            resources["VisualWorkspaceChordPartFillBrush"] = Brush(Color.FromArgb(0x40, 0x2A, 0x4A, 0x7A));
            resources["VisualWorkspaceStrongAccentBorderBrush"] = Brush(Color.FromArgb(0xCC, 0x2A, 0x4A, 0x7A));
            resources["VisualWorkspaceOverlayLabelPrimaryForegroundBrush"] = Brush(Color.FromRgb(0x24, 0x22, 0x1E));
            resources["VisualWorkspaceOverlayLabelChipBackgroundBrush"] = Brush(Color.FromArgb(0xE0, 0xED, 0xEA, 0xE3));
            resources["VisualWorkspaceOverlayLabelChipBorderBrush"] = Brush(Color.FromArgb(0x99, 0x80, 0x7A, 0x72));
            resources["VisualWorkspaceOverlayLabelChipExtraMappingsBackgroundBrush"] =
                Brush(Color.FromArgb(0xD8, 0xDD, 0xE8, 0xF8));
            resources["VisualWorkspaceOverlayLabelChipExtraMappingsBorderBrush"] =
                Brush(Color.FromArgb(0xB0, 0x2A, 0x4A, 0x7A));
            resources["VisualWorkspaceOverlayLabelChipHoverBackgroundBrush"] = Brush(Color.FromArgb(0xF0, 0xD5, 0xD0, 0xC6));
        }
        else
        {
            resources["VisualWorkspacePanelBackgroundBrush"] = Brush(Color.FromRgb(0x25, 0x25, 0x26));
            resources["VisualWorkspacePanelBorderBrush"] = Brush(Color.FromRgb(0x3F, 0x3F, 0x46));
            resources["VisualWorkspaceListItemBackgroundBrush"] = Brush(Color.FromRgb(0x2D, 0x2D, 0x30));
            resources["VisualWorkspaceListItemSelectedBackgroundBrush"] = Brush(Color.FromRgb(0x2A, 0x3F, 0x5A));
            resources["VisualWorkspaceListAccentBrush"] = Brush(Color.FromRgb(0x00, 0x7A, 0xCC));
            resources["VisualWorkspaceListItemHoverBorderBrush"] = Brush(Color.FromRgb(0x50, 0x50, 0x50));
            resources["VisualWorkspaceControllerBackgroundBrush"] = Brush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            resources["VisualWorkspaceHintBarBackgroundBrush"] = Brush(Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E));
            resources["VisualWorkspaceHintBarBorderBrush"] = Brush(Color.FromRgb(0x3F, 0x3F, 0x46));
            resources["VisualWorkspaceHintBarPrimaryForegroundBrush"] = Brushes.White;
            resources["VisualWorkspaceHintBarSecondaryForegroundBrush"] = Brush(Color.FromRgb(0xAA, 0xAA, 0xAA));
            resources["VisualWorkspaceOverlayLeaderMutedStrokeBrush"] = Brush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
            resources["VisualWorkspaceOverlayLabelDotBrush"] = Brush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
            resources["VisualWorkspacePathHoverFillBrush"] = Brush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
            resources["VisualWorkspacePathSelectedFillBrush"] = Brush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            resources["VisualWorkspaceChordPartFillBrush"] = Brush(Color.FromArgb(0x15, 0x00, 0x7A, 0xCC));
            resources["VisualWorkspaceStrongAccentBorderBrush"] = Brush(Color.FromArgb(0xCC, 0x00, 0x7A, 0xCC));
            resources["VisualWorkspaceOverlayLabelPrimaryForegroundBrush"] = Brushes.White;
            resources["VisualWorkspaceOverlayLabelChipBackgroundBrush"] = Brush(Color.FromArgb(0x45, 0x28, 0x28, 0x28));
            resources["VisualWorkspaceOverlayLabelChipBorderBrush"] = Brush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
            resources["VisualWorkspaceOverlayLabelChipExtraMappingsBackgroundBrush"] =
                Brush(Color.FromArgb(0x40, 0x2A, 0x3F, 0x5C));
            resources["VisualWorkspaceOverlayLabelChipExtraMappingsBorderBrush"] =
                Brush(Color.FromArgb(0x66, 0x47, 0x85, 0xC4));
            resources["VisualWorkspaceOverlayLabelChipHoverBackgroundBrush"] = Brush(Color.FromArgb(0x60, 0x2D, 0x2D, 0x30));
        }
    }

    private static SolidColorBrush Brush(Color color) => new(color);
}
