#nullable enable

using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.ViewModels;

public partial class AutomationRoiPreviewViewModel : ObservableObject
{
    private readonly AutomationWorkspaceViewModel _workspace;
    private readonly IAutomationRoiPreviewImageProvider _imageProvider;
    private readonly DispatcherTimer _liveTimer;
    private FormatConvertedBitmap? _pbgraCache;

    public AutomationRoiPreviewViewModel(
        AutomationWorkspaceViewModel workspace,
        IAutomationRoiPreviewImageProvider imageProvider)
    {
        _workspace = workspace;
        _imageProvider = imageProvider;
        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
        _liveTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(AutomationCapturePreviewDefaults.LiveRefreshIntervalMilliseconds),
            DispatcherPriority.Normal,
            OnLiveTick,
            Dispatcher.CurrentDispatcher);
        ReloadFromWorkspace();
    }

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private double _zoom = 1;

    [ObservableProperty]
    private bool _isLiveRefresh;

    [ObservableProperty]
    private bool _showPixelGrid;

    [ObservableProperty]
    private string _roiSummaryText = "";

    [ObservableProperty]
    private string _cursorPixelText = "";

    [ObservableProperty]
    private string _sourceHintText = "";

    [ObservableProperty]
    private double _viewportWidth = 640;

    [ObservableProperty]
    private double _viewportHeight = 480;

    public double ImageLayoutWidth => PreviewImage is BitmapSource b ? b.PixelWidth : 0;

    public double ImageLayoutHeight => PreviewImage is BitmapSource b ? b.PixelHeight : 0;

    partial void OnPreviewImageChanged(ImageSource? value)
    {
        _pbgraCache = null;
        if (value is BitmapSource bm)
        {
            var c = new FormatConvertedBitmap(bm, PixelFormats.Pbgra32, null, 0);
            if (c.CanFreeze)
                c.Freeze();
            _pbgraCache = c;
        }

        OnPropertyChanged(nameof(ImageLayoutWidth));
        OnPropertyChanged(nameof(ImageLayoutHeight));
        FitCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLiveRefreshChanged(bool value)
    {
        if (value)
        {
            _liveTimer.Start();
            RefreshLiveCore();
        }
        else
        {
            _liveTimer.Stop();
            ReloadFromWorkspace();
        }
    }

    public void Detach()
    {
        _workspace.PropertyChanged -= OnWorkspacePropertyChanged;
        _liveTimer.IsEnabled = false;
    }

    public void NotifyViewportSize(double width, double height)
    {
        ViewportWidth = width;
        ViewportHeight = height;
    }

    public void AdjustZoomFromWheel(double delta)
    {
        var factor = delta > 0 ? 1.12 : 1.0 / 1.12;
        Zoom = Math.Clamp(Zoom * factor, 0.125, 16);
    }

    public void ClearCursorSample() => CursorPixelText = "";

    public void UpdateCursorSample(double localX, double localY)
    {
        if (_pbgraCache is null)
        {
            CursorPixelText = "";
            return;
        }

        var ix = (int)Math.Floor(localX);
        var iy = (int)Math.Floor(localY);
        if (ix < 0 || iy < 0 || ix >= _pbgraCache.PixelWidth || iy >= _pbgraCache.PixelHeight)
        {
            CursorPixelText = "";
            return;
        }

        var rect = new Int32Rect(ix, iy, 1, 1);
        var px = new byte[4];
        try
        {
            _pbgraCache.CopyPixels(rect, px, 4, 0);
        }
        catch
        {
            CursorPixelText = "";
            return;
        }

        var r = px[2];
        var g = px[1];
        var b = px[0];
        var a = px[3];
        CursorPixelText = string.Format(L("AutomationRoiPreview_PixelSampleFormat"), r, g, b, a);
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AutomationWorkspaceViewModel.SelectedNode)
            or nameof(AutomationWorkspaceViewModel.RoiInspectorThumbnail))
        {
            if (!IsLiveRefresh)
                ReloadFromWorkspace();
            else
                UpdateSummaryOnly();
        }
    }

    private void OnLiveTick(object? sender, EventArgs e)
    {
        if (IsLiveRefresh)
            RefreshLiveCore();
    }

    private void UpdateSummaryOnly()
    {
        if (!TryGetCapturePreviewContext(out var props, out var blockedReason))
        {
            RoiSummaryText = FormatBlockMessage(blockedReason);
            SourceHintText = "";
            return;
        }

        RoiSummaryText = AutomationCapturePreviewSupport.FormatCaptureStatus(props!, L);
        SourceHintText = ComputeSourceHint(props);
    }

    public void ReloadFromWorkspace()
    {
        if (IsLiveRefresh)
            return;

        if (!TryGetCapturePreviewContext(out var props, out var blockedReason))
        {
            PreviewImage = null;
            RoiSummaryText = FormatBlockMessage(blockedReason);
            SourceHintText = "";
            return;
        }

        RoiSummaryText = AutomationCapturePreviewSupport.FormatCaptureStatus(props!, L);
        SourceHintText = ComputeSourceHint(props);

        var mode = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = AutomationCaptureMode.Full;

        if (string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase))
        {
            PreviewImage = _imageProvider.TryCaptureLivePreview(props) ?? _imageProvider.TryLoadStoredPreview(props);
            return;
        }

        var snapshot = _imageProvider.TryCaptureLivePreview(props);
        PreviewImage = snapshot;
        if (snapshot is null)
            SourceHintText = L("AutomationRoiPreview_FullModeSnapshotFailed");
    }

    private void RefreshLiveCore()
    {
        if (!TryGetCapturePreviewContext(out var props, out _) || props is null)
            return;

        var live = _imageProvider.TryCaptureLivePreview(props);
        if (live is null)
        {
            var mode = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureMode);
            if (string.IsNullOrWhiteSpace(mode))
                mode = AutomationCaptureMode.Full;
            if (!string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase))
                SourceHintText = L("AutomationRoiPreview_FullModeSnapshotFailed");
            return;
        }

        PreviewImage = live;
        RoiSummaryText = AutomationCapturePreviewSupport.FormatCaptureStatus(props, L);
        SourceHintText = ComputeLiveSourceHint(props);
    }

    private static string FormatBlockMessage(AutomationCapturePreviewBlockReason reason) =>
        reason == AutomationCapturePreviewBlockReason.CacheReference
            ? LStatic("AutomationRoiPreview_NoPreviewCacheRef")
            : LStatic("AutomationRoiPreview_NoCapture");

    private bool TryGetCapturePreviewContext(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsonObject? props,
        out AutomationCapturePreviewBlockReason blockedReason)
    {
        var node = _workspace.SelectedNode;
        return AutomationCapturePreviewSupport.TryGetPreviewableProperties(
            node?.NodeTypeId,
            node?.State.Properties,
            out props,
            out blockedReason);
    }

    private static string ComputeSourceHint(JsonObject? props)
    {
        if (props is null)
            return "";

        var mode = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = AutomationCaptureMode.Full;

        if (!string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase))
            return LStatic("AutomationRoiPreview_StoredFullModeHint");

        return "";
    }

    private static string ComputeLiveSourceHint(JsonObject props)
    {
        var mode = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = AutomationCaptureMode.Full;

        if (string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase))
            return LStatic("AutomationRoiPreview_LiveRoiHint");

        var sourceMode = AutomationCaptureSourceMode.Normalize(
            AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureSourceMode));

        if (AutomationCaptureSourceMode.IsInProcessWindow(sourceMode))
        {
            var target = ReadProcessTarget(props);
            var label = target.IsEmpty
                ? LStatic("AutomationRoiPreview_TargetProcessUnspecifiedLabel")
                : target.DisplayName;
            return string.Format(LStatic("AutomationRoiPreview_LiveSourceProcessFormat"), label);
        }

        return LStatic("AutomationRoiPreview_LiveSourceVirtualScreen");
    }

    private static AutomationProcessWindowTarget ReadProcessTarget(JsonObject props)
    {
        var processName = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureProcessName);
        var processId = AutomationNodePropertyReader.ReadInt(props, AutomationNodePropertyKeys.CaptureProcessId, 0);
        return AutomationProcessWindowTarget.From(processName, processId);
    }

    private static string L(string key) => AppUiLocalization.GetString(key);

    private static string LStatic(string key) => AppUiLocalization.GetString(key);

    [RelayCommand(CanExecute = nameof(CanFit))]
    private void Fit()
    {
        if (PreviewImage is not BitmapSource bmp || ViewportWidth <= 2 || ViewportHeight <= 2)
            return;

        var zw = ViewportWidth / bmp.PixelWidth;
        var zh = ViewportHeight / bmp.PixelHeight;
        Zoom = Math.Clamp(Math.Min(zw, zh), 0.125, 16);
    }

    private bool CanFit() => PreviewImage is not null;

    [RelayCommand]
    private void ActualPixels() => Zoom = 1;

    [RelayCommand]
    private void CopyRoiInfo()
    {
        if (!TryGetCapturePreviewContext(out var props, out _) || props is null)
            return;

        try
        {
            Clipboard.SetText(AutomationCapturePreviewSupport.FormatCaptureStatus(props, L));
        }
        catch
        {
        }
    }

    [RelayCommand]
    private void RefreshStored()
    {
        if (IsLiveRefresh)
            IsLiveRefresh = false;
        else
            ReloadFromWorkspace();
    }

    [RelayCommand]
    private void RefreshLive()
    {
        RefreshLiveCore();
    }
}
