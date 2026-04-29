#nullable enable

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using System.Buffers;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationScreenCaptureDesktopDuplicationService : IAutomationScreenCaptureService
{
    private static readonly TimeSpan OutputTopologyRefreshInterval = TimeSpan.FromSeconds(2);
    private const int AcquireNextFrameTimeoutMs = 5;

    private readonly AutomationScreenCaptureGdiService _gdi;
    private readonly object _sync = new();

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGIOutputDuplication? _duplication;
    private int _adapterIndex = -1;
    private int _outputIndex = -1;
    private DateTime _outputTopologyRefreshedUtc;
    private IReadOnlyList<OutputTopologyEntry> _outputTopology = [];
    private ID3D11Texture2D? _stagingTexture;
    private int _stagingWidth;
    private int _stagingHeight;
    private Format _stagingFormat = Format.Unknown;
    private byte[]? _pixelBuffer;
    private BitmapSource? _lastSuccessfulBitmap;
    private int _lastSuccessfulReqX;
    private int _lastSuccessfulReqY;
    private int _lastSuccessfulReqWidth;
    private int _lastSuccessfulReqHeight;

    public AutomationScreenCaptureDesktopDuplicationService(AutomationScreenCaptureGdiService gdi)
    {
        _gdi = gdi;
    }

    public AutomationVirtualScreenCaptureResult CaptureVirtualScreenPhysical()
    {
        lock (_sync)
        {
            if (CountDxgiOutputsUnsafe() != 1)
                return _gdi.CaptureVirtualScreenPhysical();

            var vs = AutomationVirtualScreenNative.GetPhysicalVirtualScreen();
            if (!TryCaptureRect(vs.PhysicalOriginX, vs.PhysicalOriginY, vs.WidthPx, vs.HeightPx, out var bitmap))
                return _gdi.CaptureVirtualScreenPhysical();

            var metrics = new AutomationVirtualScreenMetrics(vs.PhysicalOriginX, vs.PhysicalOriginY, bitmap.PixelWidth,
                bitmap.PixelHeight);
            try
            {
                return new AutomationVirtualScreenCaptureResult(
                    AutomationBitmapDpiNormalizer.NormalizeToDefaultDpi(bitmap),
                    metrics);
            }
            catch
            {
                return new AutomationVirtualScreenCaptureResult(bitmap, metrics);
            }
        }
    }

    public AutomationVirtualScreenCaptureResult CaptureProcessWindowPhysical(string? processName) =>
        _gdi.CaptureProcessWindowPhysical(processName);

    public AutomationVirtualScreenCaptureResult CaptureProcessWindowPhysical(AutomationProcessWindowTarget processTarget) =>
        _gdi.CaptureProcessWindowPhysical(processTarget);

    public BitmapSource CaptureRectanglePhysical(int physicalOriginX, int physicalOriginY, int widthPx, int heightPx)
    {
        if (widthPx <= 0 || heightPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(widthPx));

        lock (_sync)
        {
            if (!TryCaptureRect(physicalOriginX, physicalOriginY, widthPx, heightPx, out var bmp))
                return _gdi.CaptureRectanglePhysical(physicalOriginX, physicalOriginY, widthPx, heightPx);

            try
            {
                return AutomationBitmapDpiNormalizer.NormalizeToDefaultDpi(bmp);
            }
            catch
            {
                return bmp;
            }
        }
    }

    private int CountDxgiOutputsUnsafe()
    {
        RefreshOutputTopologyUnsafe();
        return _outputTopology.Count;
    }

    private bool TryCaptureRect(int reqX, int reqY, int reqW, int reqH, out BitmapSource bitmap)
    {
        bitmap = default!;
        if (!TryFindContainingOutput(reqX, reqY, reqW, reqH, out var aIdx, out var oIdx, out var desktopLeft,
                out var desktopTop))
            return false;

        if (!EnsureDuplication(aIdx, oIdx))
            return false;

        var left = desktopLeft;
        var top = desktopTop;
        var srcX = reqX - left;
        var srcY = reqY - top;

        var dup = _duplication!;
        var result = dup.AcquireNextFrame(AcquireNextFrameTimeoutMs, out _, out var desktopResource);
        try
        {
            if (result.Failure)
                return TryGetCachedBitmap(reqX, reqY, reqW, reqH, out bitmap);

            using (desktopResource)
            {
                using var tex = desktopResource.QueryInterface<ID3D11Texture2D>();
                var desc = tex.Description;

                EnsureStagingTexture(reqW, reqH, desc.Format);
                var box = new Box(srcX, srcY, 0, srcX + reqW, srcY + reqH, 1);
                _context!.CopySubresourceRegion(_stagingTexture!, 0, 0, 0, 0, tex, 0, box);

                _context.Map(_stagingTexture!, 0, MapMode.Read, global::Vortice.Direct3D11.MapFlags.None,
                    out var mappedSubresource);
                try
                {
                    var stride = reqW * 4;
                    var bytes = GetOrCreatePixelBuffer(stride * reqH);
                    var srcPtr = mappedSubresource.DataPointer;
                    var pitch = (int)mappedSubresource.RowPitch;
                    for (var row = 0; row < reqH; row++)
                        Marshal.Copy(IntPtr.Add(srcPtr, row * pitch), bytes, row * stride, stride);

                    var bs = BitmapSource.Create(reqW, reqH, 96, 96, PixelFormats.Bgra32, null, bytes, stride);
                    bs.Freeze();
                    bitmap = bs;
                    RememberSuccessfulBitmap(reqX, reqY, reqW, reqH, bs);
                    return true;
                }
                finally
                {
                    _context.Unmap(_stagingTexture!, 0);
                }
            }
        }
        finally
        {
            try
            {
                dup.ReleaseFrame();
            }
            catch
            {
            }
        }
    }

    private void RefreshOutputTopologyUnsafe()
    {
        var nowUtc = DateTime.UtcNow;
        if (_outputTopology.Count > 0 &&
            nowUtc - _outputTopologyRefreshedUtc < OutputTopologyRefreshInterval)
        {
            return;
        }

        try
        {
            var entries = new List<OutputTopologyEntry>();
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            for (uint adapterIdx = 0;; adapterIdx++)
            {
                if (factory.EnumAdapters1(adapterIdx, out var adapter).Failure)
                    break;
                using (adapter)
                {
                    for (uint outputIdx = 0;; outputIdx++)
                    {
                        if (adapter.EnumOutputs(outputIdx, out var output).Failure)
                            break;
                        using (output)
                        {
                            var desktop = output.Description.DesktopCoordinates;
                            entries.Add(new OutputTopologyEntry(
                                (int)adapterIdx,
                                (int)outputIdx,
                                desktop.Left,
                                desktop.Top,
                                desktop.Right,
                                desktop.Bottom));
                        }
                    }
                }
            }

            _outputTopology = entries;
            _outputTopologyRefreshedUtc = nowUtc;
        }
        catch
        {
            _outputTopology = [];
            _outputTopologyRefreshedUtc = nowUtc;
        }
    }

    private byte[] GetOrCreatePixelBuffer(int length)
    {
        if (_pixelBuffer is null || _pixelBuffer.Length < length)
            _pixelBuffer = ArrayPool<byte>.Shared.Rent(length);
        return _pixelBuffer;
    }

    private void EnsureStagingTexture(int w, int h, Format format)
    {
        if (_stagingTexture is not null &&
            _stagingWidth == w &&
            _stagingHeight == h &&
            _stagingFormat == format)
        {
            return;
        }

        _stagingTexture?.Dispose();
        _stagingTexture = CreateStagingTexture(w, h, format);
        _stagingWidth = w;
        _stagingHeight = h;
        _stagingFormat = format;
    }

    private void RememberSuccessfulBitmap(int reqX, int reqY, int reqW, int reqH, BitmapSource bitmap)
    {
        _lastSuccessfulBitmap = bitmap;
        _lastSuccessfulReqX = reqX;
        _lastSuccessfulReqY = reqY;
        _lastSuccessfulReqWidth = reqW;
        _lastSuccessfulReqHeight = reqH;
    }

    private bool TryGetCachedBitmap(int reqX, int reqY, int reqW, int reqH, out BitmapSource bitmap)
    {
        if (_lastSuccessfulBitmap is not null &&
            _lastSuccessfulReqX == reqX &&
            _lastSuccessfulReqY == reqY &&
            _lastSuccessfulReqWidth == reqW &&
            _lastSuccessfulReqHeight == reqH)
        {
            bitmap = _lastSuccessfulBitmap;
            return true;
        }

        bitmap = default!;
        return false;
    }

    private ID3D11Texture2D CreateStagingTexture(int w, int h, Format format)
    {
        var td = new Texture2DDescription
        {
            Width = (uint)w,
            Height = (uint)h,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        };
        return _device!.CreateTexture2D(td);
    }

    private bool EnsureDuplication(int adapterIndex, int outputIndex)
    {
        if (_duplication is not null && _adapterIndex == adapterIndex && _outputIndex == outputIndex)
            return true;

        _duplication?.Dispose();
        _duplication = null;

        if (_adapterIndex != adapterIndex)
        {
            _context?.Dispose();
            _device?.Dispose();
            _stagingTexture?.Dispose();
            _context = null;
            _device = null;
            _stagingTexture = null;
            _stagingWidth = 0;
            _stagingHeight = 0;
            _stagingFormat = Format.Unknown;
        }

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        if (factory.EnumAdapters1((uint)adapterIndex, out var adapter).Failure)
            return false;
        using (adapter)
        {
            if (adapter.EnumOutputs((uint)outputIndex, out var output).Failure)
                return false;
            using (output)
            {
                using var o1 = output.QueryInterface<IDXGIOutput1>();
                if (_device is null)
                {
                    if (D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport,
                            [FeatureLevel.Level_11_0], out var dev, out _, out var ctx).Failure)
                        return false;
                    _device = dev;
                    _context = ctx;
                }

                var dup = o1.DuplicateOutput(_device!);
                _duplication = dup;
                _adapterIndex = adapterIndex;
                _outputIndex = outputIndex;
                return true;
            }
        }
    }

    private bool TryFindContainingOutput(int reqX, int reqY, int reqW, int reqH,
        out int adapterIndex, out int outputIndex, out int desktopLeft, out int desktopTop)
    {
        adapterIndex = -1;
        outputIndex = -1;
        desktopLeft = 0;
        desktopTop = 0;

        RefreshOutputTopologyUnsafe();
        var reqRight = reqX + reqW;
        var reqBottom = reqY + reqH;

        foreach (var entry in _outputTopology)
        {
            if (reqX >= entry.Left &&
                reqY >= entry.Top &&
                reqRight <= entry.Right &&
                reqBottom <= entry.Bottom)
            {
                adapterIndex = entry.AdapterIndex;
                outputIndex = entry.OutputIndex;
                desktopLeft = entry.Left;
                desktopTop = entry.Top;
                return true;
            }
        }

        return false;
    }

    private readonly record struct OutputTopologyEntry(
        int AdapterIndex,
        int OutputIndex,
        int Left,
        int Top,
        int Right,
        int Bottom);
}
