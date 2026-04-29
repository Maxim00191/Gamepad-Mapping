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

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationScreenCaptureDesktopDuplicationService : IAutomationScreenCaptureService
{
    private readonly AutomationScreenCaptureGdiService _gdi;
    private readonly object _sync = new();

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGIOutputDuplication? _duplication;
    private int _adapterIndex = -1;
    private int _outputIndex = -1;

    public AutomationScreenCaptureDesktopDuplicationService(AutomationScreenCaptureGdiService gdi)
    {
        _gdi = gdi;
    }

    public AutomationVirtualScreenCaptureResult CaptureVirtualScreenPhysical()
    {
        if (CountDxgiOutputs() != 1)
            return _gdi.CaptureVirtualScreenPhysical();

        var vs = AutomationVirtualScreenNative.GetPhysicalVirtualScreen();
        lock (_sync)
        {
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

    private static int CountDxgiOutputs()
    {
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            var count = 0;
            for (uint i = 0;; i++)
            {
                if (factory.EnumAdapters1(i, out var adapter).Failure)
                    break;
                using (adapter)
                {
                    for (uint j = 0;; j++)
                    {
                        if (adapter.EnumOutputs(j, out var output).Failure)
                            break;
                        output.Dispose();
                        count++;
                    }
                }
            }

            return count;
        }
        catch
        {
            return -1;
        }
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
        var result = dup.AcquireNextFrame(250, out _, out var desktopResource);
        try
        {
            if (result.Failure)
                return false;

            using (desktopResource)
            {
                using var tex = desktopResource.QueryInterface<ID3D11Texture2D>();
                var desc = tex.Description;

                using var staging = CreateStagingTexture(reqW, reqH, desc.Format);
                var box = new Box(srcX, srcY, 0, srcX + reqW, srcY + reqH, 1);
                _context!.CopySubresourceRegion(staging, 0, 0, 0, 0, tex, 0, box);

                _context.Map(staging, 0, MapMode.Read, global::Vortice.Direct3D11.MapFlags.None,
                    out var mappedSubresource);
                try
                {
                    var stride = reqW * 4;
                    var bytes = new byte[stride * reqH];
                    var srcPtr = mappedSubresource.DataPointer;
                    var pitch = (int)mappedSubresource.RowPitch;
                    for (var row = 0; row < reqH; row++)
                        Marshal.Copy(IntPtr.Add(srcPtr, row * pitch), bytes, row * stride, stride);

                    var bs = BitmapSource.Create(reqW, reqH, 96, 96, PixelFormats.Bgra32, null, bytes, stride);
                    bs.Freeze();
                    bitmap = bs;
                    return true;
                }
                finally
                {
                    _context.Unmap(staging, 0);
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
            _context = null;
            _device = null;
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

    private static bool TryFindContainingOutput(int reqX, int reqY, int reqW, int reqH,
        out int adapterIndex, out int outputIndex, out int desktopLeft, out int desktopTop)
    {
        adapterIndex = -1;
        outputIndex = -1;
        desktopLeft = 0;
        desktopTop = 0;

        var reqRight = reqX + reqW;
        var reqBottom = reqY + reqH;

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        for (uint i = 0;; i++)
        {
            if (factory.EnumAdapters1(i, out var adapter).Failure)
                break;
            using (adapter)
            {
                for (uint j = 0;; j++)
                {
                    if (adapter.EnumOutputs(j, out var output).Failure)
                        break;
                    using (output)
                    {
                        var d = output.Description.DesktopCoordinates;
                        if (reqX >= d.Left && reqY >= d.Top && reqRight <= d.Right && reqBottom <= d.Bottom)
                        {
                            adapterIndex = (int)i;
                            outputIndex = (int)j;
                            desktopLeft = d.Left;
                            desktopTop = d.Top;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
