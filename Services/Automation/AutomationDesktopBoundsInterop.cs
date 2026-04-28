#nullable enable

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationDesktopBoundsInterop
{
    private const uint SwpNozorder = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool PhysicalToLogicalPointForPerMonitorDpi(IntPtr hwnd, ref POINT lpPoint);

    /// <summary>
    /// Places the Win32 window at physical screen pixels matching GDI BitBlt / GetSystemMetrics virtual-screen
    /// bounds. Requires per-monitor DPI awareness so coordinates are interpreted as physical pixels.
    /// </summary>
    public static bool TryPositionWindowPhysical(IntPtr hwnd, int physicalX, int physicalY, int physicalWidth,
        int physicalHeight)
    {
        if (hwnd == IntPtr.Zero || physicalWidth <= 0 || physicalHeight <= 0)
            return false;

        try
        {
            return SetWindowPos(hwnd, IntPtr.Zero, physicalX, physicalY, physicalWidth, physicalHeight, SwpNozorder);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (SEHException)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static bool TryGetWindowClientScreenMetrics(IntPtr hwnd, out int originScreenX, out int originScreenY,
        out int widthPx, out int heightPx)
    {
        originScreenX = originScreenY = widthPx = heightPx = 0;

        if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out var rc))
            return false;

        var tl = new POINT { X = rc.Left, Y = rc.Top };
        var br = new POINT { X = rc.Right, Y = rc.Bottom };
        if (!ClientToScreen(hwnd, ref tl) || !ClientToScreen(hwnd, ref br))
            return false;

        originScreenX = tl.X;
        originScreenY = tl.Y;
        widthPx = Math.Max(1, Math.Abs(br.X - tl.X));
        heightPx = Math.Max(1, Math.Abs(br.Y - tl.Y));
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    public static bool TryApplyPhysicalRectAsWpfWindowBounds(Window window, int physicalOriginX, int physicalOriginY,
        int physicalWidthPx, int physicalHeightPx)
    {
        if (physicalWidthPx <= 0 || physicalHeightPx <= 0)
            return false;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        var tl = new POINT { X = physicalOriginX, Y = physicalOriginY };
        var br = new POINT { X = physicalOriginX + physicalWidthPx, Y = physicalOriginY + physicalHeightPx };
        if (!PhysicalToLogicalPointForPerMonitorDpi(hwnd, ref tl) || !PhysicalToLogicalPointForPerMonitorDpi(hwnd, ref br))
            return false;

        var left = Math.Min(tl.X, br.X);
        var top = Math.Min(tl.Y, br.Y);
        var width = Math.Abs(br.X - tl.X);
        var height = Math.Abs(br.Y - tl.Y);
        if (width < 1 || height < 1)
            return false;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = left;
        window.Top = top;
        window.Width = width;
        window.Height = height;
        return true;
    }

    public static bool TryPhysicalPointToLogical(int physicalX, int physicalY, out double logicalX,
        out double logicalY)
    {
        logicalX = 0;
        logicalY = 0;

        try
        {
            var p = new POINT { X = physicalX, Y = physicalY };
            if (!PhysicalToLogicalPointForPerMonitorDpi(IntPtr.Zero, ref p))
                return false;

            logicalX = p.X;
            logicalY = p.Y;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (SEHException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
