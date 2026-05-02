#nullable enable

using System.Windows;
using System.Windows.Media;

namespace Gamepad_Mapping.Utils.Theme;

internal static class AutomationWorkspaceTheme
{
    public static void Apply(ResourceDictionary resources, bool light)
    {
        if (light)
        {
            resources["AutomationWorkspaceCanvasBackdropBrush"] = Brush(Color.FromRgb(0xC8, 0xC4, 0xBC));
            resources["AutomationWorkspaceNodeBodyBrush"] = Brush(Color.FromRgb(0xF3, 0xF1, 0xEC));
            resources["AutomationWorkspaceGridDotBrush"] = Brush(Color.FromArgb(0x48, 0x50, 0x4C, 0x46));
            resources["AutomationWorkspaceToolbarBackgroundBrush"] = Brush(Color.FromRgb(0xE6, 0xE2, 0xDA));
            resources["AutomationWorkspaceToolbarBorderBrush"] = Brush(Color.FromArgb(0x99, 0x80, 0x7A, 0x72));
            resources["AutomationWorkspaceMinimapPanelBackgroundBrush"] = Brush(Color.FromArgb(0xD8, 0x36, 0x36, 0x38));
            resources["AutomationWorkspaceMinimapViewportFillBrush"] = Brush(Color.FromArgb(0x35, 0x42, 0x7A, 0xCC));
            resources["AutomationWorkspaceMinimapViewportStrokeBrush"] = Brush(Color.FromArgb(0xEE, 0x50, 0x90, 0xE8));
            resources["AutomationWorkspaceMinimapNodeFillBrush"] = Brush(Color.FromArgb(0x78, 0x90, 0x90, 0x90));
            resources["AutomationWorkspaceMinimapNodeStrokeBrush"] = Brush(Color.FromArgb(0xB0, 0x70, 0x70, 0x70));
            resources["AutomationWorkspaceMinimapNodeSelectedFillBrush"] = Brush(Color.FromArgb(0xA0, 0xE4, 0xB5, 0x4A));
            resources["AutomationWorkspaceMinimapNodeSelectedStrokeBrush"] = Brush(Color.FromArgb(0xFF, 0xE4, 0xB5, 0x4A));
            resources["AutomationWorkspaceMinimapHostBorderBrush"] = Brush(Color.FromArgb(0x99, 0x80, 0x80, 0x80));
            resources["AutomationWorkspaceMinimapHostBackgroundBrush"] = Brush(Color.FromArgb(0x28, 0x20, 0x20, 0x20));
            resources["AutomationWorkspaceSelectionRectFillBrush"] = Brush(Color.FromArgb(0x30, 0x52, 0x90, 0xE8));
            resources["AutomationWorkspacePortLabelForegroundBrush"] = Brush(Color.FromRgb(0x24, 0x22, 0x1E));
        }
        else
        {
            resources["AutomationWorkspaceCanvasBackdropBrush"] = Brush(Color.FromRgb(0x1A, 0x1A, 0x1D));
            resources["AutomationWorkspaceNodeBodyBrush"] = Brush(Color.FromRgb(0x2C, 0x2C, 0x32));
            resources["AutomationWorkspaceGridDotBrush"] = Brush(Color.FromArgb(0x55, 0xAA, 0xAA, 0xB0));
            resources["AutomationWorkspaceToolbarBackgroundBrush"] = Brush(Color.FromRgb(0x28, 0x28, 0x2E));
            resources["AutomationWorkspaceToolbarBorderBrush"] = Brush(Color.FromRgb(0x50, 0x50, 0x58));
            resources["AutomationWorkspaceMinimapPanelBackgroundBrush"] = Brush(Color.FromArgb(0xE5, 0x22, 0x22, 0x26));
            resources["AutomationWorkspaceMinimapViewportFillBrush"] = Brush(Color.FromArgb(0x35, 0x42, 0x7A, 0xCC));
            resources["AutomationWorkspaceMinimapViewportStrokeBrush"] = Brush(Color.FromArgb(0xEE, 0x5C, 0xA8, 0xFF));
            resources["AutomationWorkspaceMinimapNodeFillBrush"] = Brush(Color.FromArgb(0x78, 0xB0, 0xB0, 0xB8));
            resources["AutomationWorkspaceMinimapNodeStrokeBrush"] = Brush(Color.FromArgb(0xB0, 0x70, 0x74, 0x80));
            resources["AutomationWorkspaceMinimapNodeSelectedFillBrush"] = Brush(Color.FromArgb(0xA0, 0xE4, 0xB5, 0x4A));
            resources["AutomationWorkspaceMinimapNodeSelectedStrokeBrush"] = Brush(Color.FromArgb(0xFF, 0xF0, 0xC8, 0x55));
            resources["AutomationWorkspaceMinimapHostBorderBrush"] = Brush(Color.FromArgb(0x66, 0x90, 0x90, 0x98));
            resources["AutomationWorkspaceMinimapHostBackgroundBrush"] = Brush(Color.FromArgb(0x45, 0x08, 0x08, 0x0C));
            resources["AutomationWorkspaceSelectionRectFillBrush"] = Brush(Color.FromArgb(0x38, 0x80, 0xC0, 0xFF));
            resources["AutomationWorkspacePortLabelForegroundBrush"] = Brush(Color.FromRgb(0xB9, 0xB9, 0xC3));
        }
    }

    private static SolidColorBrush Brush(Color color) => new(color);
}
