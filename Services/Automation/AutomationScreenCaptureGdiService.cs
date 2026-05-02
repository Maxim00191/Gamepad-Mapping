using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationScreenCaptureGdiService : IAutomationScreenCaptureService
{
    private const uint Srccopy = 0x00CC0020;
    private readonly IAutomationProcessWindowResolver _processWindowResolver;

    public AutomationScreenCaptureGdiService(IAutomationProcessWindowResolver? processWindowResolver = null)
    {
        _processWindowResolver = processWindowResolver ?? new AutomationProcessWindowResolver();
    }

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
        return CaptureProcessWindowPhysical(AutomationProcessWindowTarget.From(processName));
    }

    public AutomationVirtualScreenCaptureResult CaptureProcessWindowPhysical(AutomationProcessWindowTarget processTarget)
    {
        using var _ = AutomationDpiAwarenessScope.EnterPerMonitorAware();
        if (!_processWindowResolver.TryResolveWindowHandle(processTarget, out var windowHandle, out var bounds, out var resolvedTarget))
            throw new InvalidOperationException("process_window_not_found");

        var width = bounds.Width;
        var height = bounds.Height;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("process_window_rect_invalid");

        var bitmap = CaptureWindowPhysicalCore(windowHandle, width, height);
        var metrics = new AutomationVirtualScreenMetrics(bounds.X, bounds.Y, bitmap.PixelWidth, bitmap.PixelHeight);
        return new AutomationVirtualScreenCaptureResult(bitmap, metrics, resolvedTarget);
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

                    var bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                        hBmp,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bitmap.Freeze();
                    try
                    {
                        return AutomationBitmapDpiNormalizer.NormalizeToDefaultDpi(bitmap);
                    }
                    catch
                    {
                        return bitmap;
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

    private static BitmapSource CaptureWindowPhysicalCore(IntPtr windowHandle, int widthPx, int heightPx)
    {
        if (windowHandle == IntPtr.Zero)
            throw new ArgumentOutOfRangeException(nameof(windowHandle));
        if (widthPx <= 0 || heightPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(widthPx));

        var windowDc = User32.GetWindowDC(windowHandle);
        if (windowDc == IntPtr.Zero)
            throw new InvalidOperationException("process_window_dc_unavailable");

        try
        {
            var memoryDc = Gdi32.CreateCompatibleDC(windowDc);
            if (memoryDc == IntPtr.Zero)
                throw new InvalidOperationException("process_window_dc_create_failed");

            try
            {
                var bitmapHandle = Gdi32.CreateCompatibleBitmap(windowDc, widthPx, heightPx);
                if (bitmapHandle == IntPtr.Zero)
                    throw new InvalidOperationException("process_window_bitmap_create_failed");

                try
                {
                    var oldObject = Gdi32.SelectObject(memoryDc, bitmapHandle);
                    var printSucceeded = User32.PrintWindow(windowHandle, memoryDc, User32.PrintWindowRenderFullContent);
                    if (!printSucceeded)
                        printSucceeded = User32.PrintWindow(windowHandle, memoryDc, 0);

                    Gdi32.SelectObject(memoryDc, oldObject);

                    if (!printSucceeded)
                        throw new InvalidOperationException("process_window_print_failed");

                    var bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                        bitmapHandle,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bitmap.Freeze();
                    try
                    {
                        return AutomationBitmapDpiNormalizer.NormalizeToDefaultDpi(bitmap);
                    }
                    catch
                    {
                        return bitmap;
                    }
                }
                finally
                {
                    Gdi32.DeleteObject(bitmapHandle);
                }
            }
            finally
            {
                Gdi32.DeleteDC(memoryDc);
            }
        }
        finally
        {
            User32.ReleaseDC(windowHandle, windowDc);
        }
    }

    private static class User32
    {
        public const uint PrintWindowRenderFullContent = 0x00000002;

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
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

}
