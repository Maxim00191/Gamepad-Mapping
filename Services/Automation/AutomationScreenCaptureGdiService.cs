using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationScreenCaptureGdiService : IAutomationScreenCaptureService
{
    private const uint Srccopy = 0x00CC0020;

    public AutomationVirtualScreenCaptureResult CaptureVirtualScreenPhysical()
    {
        using var _ = AutomationDpiAwarenessScope.EnterPerMonitorAware();
        return CaptureVirtualScreenPhysicalCore();
    }

    public BitmapSource CaptureRectanglePhysical(int physicalOriginX, int physicalOriginY, int widthPx, int heightPx)
    {
        using var _ = AutomationDpiAwarenessScope.EnterPerMonitorAware();
        return CaptureRectanglePhysicalCore(physicalOriginX, physicalOriginY, widthPx, heightPx);
    }

    public AutomationVirtualScreenCaptureResult CaptureProcessWindowPhysical(string? processName)
    {
        using var _ = AutomationDpiAwarenessScope.EnterPerMonitorAware();
        var windowHandle = ResolveTargetWindowHandle(processName);
        if (windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("process_window_not_found");

        if (!User32.GetWindowRect(windowHandle, out var rect))
            throw new InvalidOperationException("process_window_rect_unavailable");

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("process_window_rect_invalid");

        var bitmap = CaptureRectanglePhysicalCore(rect.Left, rect.Top, width, height);
        var metrics = new AutomationVirtualScreenMetrics(rect.Left, rect.Top, bitmap.PixelWidth, bitmap.PixelHeight);
        return new AutomationVirtualScreenCaptureResult(bitmap, metrics);
    }

    private AutomationVirtualScreenCaptureResult CaptureVirtualScreenPhysicalCore()
    {
        var requested = AutomationVirtualScreenNative.GetPhysicalVirtualScreen();
        var bmp = CaptureRectanglePhysicalCore(requested.PhysicalOriginX, requested.PhysicalOriginY, requested.WidthPx,
            requested.HeightPx);
        var aligned = new AutomationVirtualScreenMetrics(requested.PhysicalOriginX, requested.PhysicalOriginY,
            bmp.PixelWidth, bmp.PixelHeight);
        return new AutomationVirtualScreenCaptureResult(bmp, aligned);
    }

    private static BitmapSource CaptureRectanglePhysicalCore(int physicalOriginX, int physicalOriginY, int widthPx,
        int heightPx)
    {
        if (widthPx <= 0 || heightPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(widthPx));

        var hdcScreen = User32.GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
            throw new InvalidOperationException("GetDC failed.");

        try
        {
            var hdcMem = Gdi32.CreateCompatibleDC(hdcScreen);
            if (hdcMem == IntPtr.Zero)
                throw new InvalidOperationException("CreateCompatibleDC failed.");

            try
            {
                var hBmp = Gdi32.CreateCompatibleBitmap(hdcScreen, widthPx, heightPx);
                if (hBmp == IntPtr.Zero)
                    throw new InvalidOperationException("CreateCompatibleBitmap failed.");

                try
                {
                    var old = Gdi32.SelectObject(hdcMem, hBmp);
                    if (!Gdi32.BitBlt(hdcMem, 0, 0, widthPx, heightPx, hdcScreen, physicalOriginX, physicalOriginY,
                            Srccopy))
                        throw new InvalidOperationException("BitBlt failed.");

                    Gdi32.SelectObject(hdcMem, old);

                    using var gdiBmp = Image.FromHbitmap(hBmp);
                    using var ms = new MemoryStream();
                    gdiBmp.Save(ms, ImageFormat.Png);
                    ms.Position = 0;

                    var bmpImage = new BitmapImage();
                    bmpImage.BeginInit();
                    bmpImage.StreamSource = ms;
                    bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                    bmpImage.EndInit();
                    bmpImage.Freeze();
                    try
                    {
                        return AutomationBitmapDpiNormalizer.NormalizeToDefaultDpi(bmpImage);
                    }
                    catch
                    {
                        return bmpImage;
                    }
                }
                finally
                {
                    Gdi32.DeleteObject(hBmp);
                }
            }
            finally
            {
                Gdi32.DeleteDC(hdcMem);
            }
        }
        finally
        {
            User32.ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    private static IntPtr ResolveTargetWindowHandle(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return User32.GetForegroundWindow();

        var normalized = NormalizeProcessName(processName);
        if (string.IsNullOrWhiteSpace(normalized))
            return User32.GetForegroundWindow();

        try
        {
            var processes = Process.GetProcessesByName(normalized);
            try
            {
                foreach (var process in processes)
                {
                    if (IsWindowCaptureCandidate(process.MainWindowHandle))
                        return process.MainWindowHandle;
                }
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
        catch
        {
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    private static string NormalizeProcessName(string processName)
    {
        var trimmed = processName.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return trimmed[..^4].Trim();
        return trimmed;
    }

    private static bool IsWindowCaptureCandidate(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !User32.IsWindowVisible(handle))
            return false;
        return User32.GetWindowRect(handle, out var rect) && rect.Right > rect.Left && rect.Bottom > rect.Top;
    }

    private static class User32
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);
    }

    private static class Gdi32
    {
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1,
            uint rop);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
