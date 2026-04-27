using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Gamepad_Mapping.Views.Automation;

public partial class AutomationMagnifierWindow : Window
{
    private readonly BitmapSource _source;
    private readonly int _zoom;
    private readonly int _halfSample;

    public AutomationMagnifierWindow(BitmapSource frozenFullScreen, int zoom)
    {
        InitializeComponent();
        _source = frozenFullScreen;
        _zoom = Math.Clamp(zoom, 4, 8);
        _halfSample = Math.Max(8, 96 / _zoom);
    }

    public void UpdateAtPhysical(int physicalScreenX, int physicalScreenY, int virtualOriginX, int virtualOriginY)
    {
        var lx = physicalScreenX - virtualOriginX;
        var ly = physicalScreenY - virtualOriginY;
        var rect = new Int32Rect(
            Math.Clamp(lx - _halfSample, 0, Math.Max(0, _source.PixelWidth - _halfSample * 2)),
            Math.Clamp(ly - _halfSample, 0, Math.Max(0, _source.PixelHeight - _halfSample * 2)),
            Math.Min(_halfSample * 2, _source.PixelWidth),
            Math.Min(_halfSample * 2, _source.PixelHeight));

        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        try
        {
            var cropped = new CroppedBitmap(_source, rect);
            cropped.Freeze();
            ZoomImage.Source = cropped;
            ZoomImage.Width = rect.Width * _zoom / 2.0;
            ZoomImage.Height = rect.Height * _zoom / 2.0;
        }
        catch
        {
            // Ignore magnifier failures during drag.
        }

        PositionNearCursor();
    }

    private void PositionNearCursor()
    {
        if (!GetCursorPos(out var pt))
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        Left = pt.X / dpi.PixelsPerInchX * 96.0 + 24;
        Top = pt.Y / dpi.PixelsPerInchY * 96.0 + 24;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
