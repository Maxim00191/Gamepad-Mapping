#nullable enable

using GamepadMapperGUI.Interfaces.Services.Input;
using HidSharp;

namespace GamepadMapperGUI.Services.Input;

public sealed class HidSharpDualSenseHidStreamFactory : IDualSenseHidStreamFactory
{
    private const int SonyVendorId = 0x054C;
    private static readonly ushort[] SupportedProductIds = [0x0CE6, 0x0DF2];

    public bool TryOpen(out IDualSenseHidStream? stream, out int maxInputReportLength)
    {
        foreach (var productId in SupportedProductIds)
        {
            var device = DeviceList.Local.GetHidDeviceOrNull(SonyVendorId, productId);
            if (device is null)
                continue;

            if (!device.TryOpen(out var hidStream))
                continue;

            stream = new HidSharpDualSenseHidStream(hidStream);
            maxInputReportLength = Math.Max(device.GetMaxInputReportLength(), 64);
            return true;
        }

        stream = null;
        maxInputReportLength = 0;
        return false;
    }
}
