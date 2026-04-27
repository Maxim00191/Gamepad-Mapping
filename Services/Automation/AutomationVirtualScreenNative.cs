using System.Runtime.InteropServices;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationVirtualScreenNative
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCXVirtualScreen = 78;
    private const int SmCYVirtualScreen = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public static AutomationVirtualScreenMetrics GetPhysicalVirtualScreen() =>
        new(GetSystemMetrics(SmXVirtualScreen), GetSystemMetrics(SmYVirtualScreen), GetSystemMetrics(SmCXVirtualScreen),
            GetSystemMetrics(SmCYVirtualScreen));
}
