using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace Gamepad_Mapping.Views.Automation;

public partial class AutomationRegionPickerWindow : Window
{
    private readonly BitmapSource _freeze;
    private AutomationVirtualScreenMetrics _vs;
    private readonly AutomationMagnifierWindow? _magnifier;

    private int _aX;
    private int _aY;
    private int _bX;
    private int _bY;
    private bool _selecting;
    private string? _resizeMode;
    private bool _hasBox;

    private AutomationPhysicalRect _moveBaseline;
    private int _movePtrX;
    private int _movePtrY;
    private bool _applyingFullscreenBounds;

    public AutomationRegionPickerWindow(BitmapSource frozenScreen, AutomationVirtualScreenMetrics captureMetrics,
        bool showMagnifier = true)
    {
        InitializeComponent();
        _freeze = frozenScreen;
        FreezeView.Source = _freeze;
        _vs = new AutomationVirtualScreenMetrics(
            captureMetrics.PhysicalOriginX,
            captureMetrics.PhysicalOriginY,
            Math.Max(1, frozenScreen.PixelWidth),
            Math.Max(1, frozenScreen.PixelHeight));
        if (showMagnifier)
        {
            _magnifier = new AutomationMagnifierWindow(frozenScreen, 4);
            _magnifier.Show();
        }

        PreviewKeyDown += OnPreviewKeyDown;
        DpiChanged += (_, _) =>
        {
            if (_applyingFullscreenBounds)
                return;
            ApplyFullscreenOverlayBounds();
            SyncFreezeViewToClientSurface();
        };
        SizeChanged += (_, _) => SyncFreezeViewToClientSurface();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyFullscreenOverlayBounds();
        SyncFreezeViewToClientSurface();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Dispatcher.BeginInvoke(ApplyFullscreenOverlayBounds, DispatcherPriority.Render);
        Dispatcher.BeginInvoke(ApplyFullscreenOverlayBounds, DispatcherPriority.ApplicationIdle);
        Dispatcher.BeginInvoke(SyncFreezeViewToClientSurface, DispatcherPriority.Render);
        Dispatcher.BeginInvoke(SyncFreezeViewToClientSurface, DispatcherPriority.ApplicationIdle);
    }

    private void ApplyFullscreenOverlayBounds()
    {
        if (_applyingFullscreenBounds)
            return;

        _applyingFullscreenBounds = true;
        try
        {
            var snapped = SnapWindowToPhysicalVirtualScreen();
            if (!snapped)
                AutomationDesktopBoundsInterop.TryApplyPhysicalRectAsWpfWindowBounds(this, _vs.PhysicalOriginX,
                    _vs.PhysicalOriginY, _freeze.PixelWidth, _freeze.PixelHeight);
        }
        finally
        {
            _applyingFullscreenBounds = false;
        }
    }

    private bool SnapWindowToPhysicalVirtualScreen()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        return AutomationDesktopBoundsInterop.TryPositionWindowPhysical(
            hwnd,
            _vs.PhysicalOriginX,
            _vs.PhysicalOriginY,
            _vs.WidthPx,
            _vs.HeightPx);
    }

    private void SyncFreezeViewToClientSurface()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        if (!AutomationDesktopBoundsInterop.TryGetWindowClientScreenMetrics(
                hwnd,
                out _,
                out _,
                out var clientWidthPx,
                out var clientHeightPx))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        if (dpi.DpiScaleX <= 0 || dpi.DpiScaleY <= 0)
            return;

        FreezeView.Width = clientWidthPx / dpi.DpiScaleX;
        FreezeView.Height = clientHeightPx / dpi.DpiScaleY;
    }

    public AutomationPhysicalRect? ResultRect { get; private set; }

    public BitmapSource? ResultCrop { get; private set; }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (_hasBox)
            {
                var committed = ClampRect(NormalizeRaw());
                ResultRect = committed;
                ResultCrop = TryCropFreeze(committed);
                DialogResult = true;
                Close();
            }

            return;
        }

        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down) || !_hasBox)
            return;

        e.Handled = true;
        var n = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? 10 : 1;
        var d = e.Key switch
        {
            Key.Left => (-n, 0),
            Key.Right => (n, 0),
            Key.Up => (0, -n),
            Key.Down => (0, n),
            _ => (0, 0)
        };

        var r = NormalizeRaw();
        _aX = r.X + d.Item1;
        _aY = r.Y + d.Item2;
        _bX = r.X + r.Width + d.Item1;
        _bY = r.Y + r.Height + d.Item2;
        UpdateSelectionVisual();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!GetCursorPos(out var p))
            return;

        if (e.OriginalSource is Ellipse el)
        {
            if (ReferenceEquals(el, HandleNw)) _resizeMode = "nw";
            else if (ReferenceEquals(el, HandleNe)) _resizeMode = "ne";
            else if (ReferenceEquals(el, HandleSw)) _resizeMode = "sw";
            else if (ReferenceEquals(el, HandleSe)) _resizeMode = "se";
            else
                return;

            _selecting = true;
            CaptureMouse();
            e.Handled = true;
            UpdateMagnifier(p.X, p.Y);
            return;
        }

        if (_hasBox && PointInBox(p.X, p.Y))
        {
            _moveBaseline = NormalizeRaw();
            _movePtrX = p.X;
            _movePtrY = p.Y;
            _resizeMode = "move";
            _selecting = true;
            CaptureMouse();
            e.Handled = true;
            UpdateMagnifier(p.X, p.Y);
            return;
        }

        _aX = p.X;
        _aY = p.Y;
        _bX = p.X;
        _bY = p.Y;
        _hasBox = false;
        _selecting = true;
        _resizeMode = "new";
        CaptureMouse();
        UpdateMagnifier(p.X, p.Y);
        e.Handled = true;
    }

    private bool PointInBox(int px, int py)
    {
        var r = NormalizeRaw();
        return px >= r.X && px <= r.X + r.Width && py >= r.Y && py <= r.Y + r.Height;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!GetCursorPos(out var p))
            return;

        UpdateMagnifier(p.X, p.Y);

        if (!_selecting)
            return;

        switch (_resizeMode)
        {
            case "new":
                _bX = p.X;
                _bY = p.Y;
                break;
            case "move":
            {
                var dx = p.X - _movePtrX;
                var dy = p.Y - _movePtrY;
                _aX = _moveBaseline.X + dx;
                _aY = _moveBaseline.Y + dy;
                _bX = _moveBaseline.X + _moveBaseline.Width + dx;
                _bY = _moveBaseline.Y + _moveBaseline.Height + dy;
                break;
            }
            case "nw":
                _aX = p.X;
                _aY = p.Y;
                break;
            case "ne":
                _bX = p.X;
                _aY = p.Y;
                break;
            case "sw":
                _aX = p.X;
                _bY = p.Y;
                break;
            case "se":
                _bX = p.X;
                _bY = p.Y;
                break;
            default:
                _bX = p.X;
                _bY = p.Y;
                break;
        }

        UpdateSelectionVisual();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting)
            return;

        _selecting = false;
        ReleaseMouseCapture();

        if (_resizeMode == "new")
        {
            if (GetCursorPos(out var p))
            {
                _bX = p.X;
                _bY = p.Y;
            }

            var nr = ClampRect(NormalizeRaw());
            _hasBox = nr.Width >= 4 && nr.Height >= 4;
            if (!_hasBox)
                SelectionBorder.Visibility = Visibility.Collapsed;

            _resizeMode = null;
            UpdateSelectionVisual();
            return;
        }

        _resizeMode = null;
        UpdateSelectionVisual();
    }

    private void UpdateMagnifier(int physicalX, int physicalY) =>
        _magnifier?.UpdateAtPhysical(physicalX, physicalY, _vs.PhysicalOriginX, _vs.PhysicalOriginY);

    private void UpdateSelectionVisual()
    {
        var nr = ClampRect(NormalizeRaw());
        if (nr.Width < 2 || nr.Height < 2)
        {
            if (_resizeMode != "new" && !_selecting)
                SelectionBorder.Visibility = Visibility.Collapsed;

            ToggleHandles(false);
            return;
        }

        SelectionBorder.Visibility = Visibility.Visible;
        ToggleHandles(_hasBox && !_selecting);

        var ow = OverlayCanvas.ActualWidth > 0 ? OverlayCanvas.ActualWidth : ActualWidth;
        var oh = OverlayCanvas.ActualHeight > 0 ? OverlayCanvas.ActualHeight : ActualHeight;

        double bx, by, bw, bh;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero &&
            AutomationDesktopBoundsInterop.TryGetWindowClientScreenMetrics(hwnd, out var cox, out var coy, out var cw,
                out var ch))
        {
            AutomationOverlayCoordinateMapping.PhysicalRectToOverlayForClientSurface(nr, cox, coy, cw, ch, ow, oh,
                out bx, out by, out bw, out bh);
        }
        else
        {
            AutomationOverlayCoordinateMapping.PhysicalRectToOverlay(nr, CaptureExtentMetrics(), ow, oh, out bx, out by,
                out bw, out bh);
        }

        Canvas.SetLeft(SelectionBorder, bx);
        Canvas.SetTop(SelectionBorder, by);
        SelectionBorder.Width = bw;
        SelectionBorder.Height = bh;

        PlaceHandle(HandleNw, bx - 6, by - 6);
        PlaceHandle(HandleNe, bx + bw - 6, by - 6);
        PlaceHandle(HandleSw, bx - 6, by + bh - 6);
        PlaceHandle(HandleSe, bx + bw - 6, by + bh - 6);

        ResultRect = nr;
    }

    private static void PlaceHandle(UIElement handle, double x, double y)
    {
        Canvas.SetLeft(handle, x);
        Canvas.SetTop(handle, y);
    }

    private void ToggleHandles(bool on)
    {
        var vis = on ? Visibility.Visible : Visibility.Collapsed;
        HandleNw.Visibility = vis;
        HandleNe.Visibility = vis;
        HandleSw.Visibility = vis;
        HandleSe.Visibility = vis;
    }

    private AutomationPhysicalRect NormalizeRaw()
    {
        var x1 = Math.Min(_aX, _bX);
        var y1 = Math.Min(_aY, _bY);
        var x2 = Math.Max(_aX, _bX);
        var y2 = Math.Max(_aY, _bY);
        return new AutomationPhysicalRect(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1));
    }

    private AutomationVirtualScreenMetrics CaptureExtentMetrics() =>
        new(_vs.PhysicalOriginX, _vs.PhysicalOriginY, _freeze.PixelWidth, _freeze.PixelHeight);

    private AutomationPhysicalRect ClampRect(AutomationPhysicalRect raw)
    {
        var m = CaptureExtentMetrics();
        var maxX = m.PhysicalOriginX + m.WidthPx;
        var maxY = m.PhysicalOriginY + m.HeightPx;
        var x1 = Math.Clamp(raw.X, m.PhysicalOriginX, maxX);
        var y1 = Math.Clamp(raw.Y, m.PhysicalOriginY, maxY);
        var x2 = Math.Clamp(raw.X + raw.Width, m.PhysicalOriginX, maxX);
        var y2 = Math.Clamp(raw.Y + raw.Height, m.PhysicalOriginY, maxY);
        return new AutomationPhysicalRect(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1));
    }

    private BitmapSource? TryCropFreeze(AutomationPhysicalRect rect)
    {
        if (rect.IsEmpty)
            return null;

        var ox = rect.X - _vs.PhysicalOriginX;
        var oy = rect.Y - _vs.PhysicalOriginY;
        if (ox < 0 || oy < 0 || ox + rect.Width > _freeze.PixelWidth || oy + rect.Height > _freeze.PixelHeight)
            return null;

        try
        {
            var cropped = new CroppedBitmap(_freeze, new Int32Rect(ox, oy, rect.Width, rect.Height));
            cropped.Freeze();
            return cropped;
        }
        catch
        {
            return null;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _magnifier?.Close();
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
