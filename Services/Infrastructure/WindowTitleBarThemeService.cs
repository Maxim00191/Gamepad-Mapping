#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class WindowTitleBarThemeService : IWindowTitleBarThemeService
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmBorderColor = 34;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    public bool TryApply(Window window, Color backgroundColor, Color foregroundColor, Color borderColor, bool usesLightTheme)
    {
        if (window is null)
            return false;

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return false;

        return TryApplyToHandle(handle, backgroundColor, foregroundColor, borderColor, usesLightTheme);
    }

    private static bool TryApplyToHandle(IntPtr handle, Color backgroundColor, Color foregroundColor, Color borderColor, bool usesLightTheme)
    {
        var applied = false;
        var darkModeEnabled = usesLightTheme ? 0 : 1;

        applied |= TrySetIntAttribute(handle, DwmUseImmersiveDarkMode, darkModeEnabled);
        applied |= TrySetIntAttribute(handle, DwmCaptionColor, ToColorRef(backgroundColor));
        applied |= TrySetIntAttribute(handle, DwmTextColor, ToColorRef(foregroundColor));
        applied |= TrySetIntAttribute(handle, DwmBorderColor, ToColorRef(borderColor));
        return applied;
    }

    private static bool TrySetIntAttribute(IntPtr handle, int attribute, int value)
    {
        return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)) == 0;
    }

    private static int ToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
