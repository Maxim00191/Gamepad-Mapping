#nullable enable

using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationProcessWindowInputDispatcher : IAutomationProcessWindowInputDispatcher
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMButtonUp = 0x0208;
    private const uint MkLButton = 0x0001;
    private const uint MkRButton = 0x0002;
    private const uint MkMButton = 0x0010;
    private readonly IAutomationProcessWindowResolver _processWindowResolver;

    public AutomationProcessWindowInputDispatcher(IAutomationProcessWindowResolver? processWindowResolver = null)
    {
        _processWindowResolver = processWindowResolver ?? new AutomationProcessWindowResolver();
    }

    public bool TryKeyDown(string processName, Key key)
    {
        return TryKeyDown(AutomationProcessWindowTarget.From(processName), key);
    }

    public bool TryKeyDown(AutomationProcessWindowTarget processTarget, Key key)
    {
        if (!_processWindowResolver.TryResolveWindowHandle(processTarget, out var hwnd, out _, out _))
            return false;
        var vkey = KeyInterop.VirtualKeyFromKey(key);
        return PostMessage(hwnd, WmKeyDown, (nuint)vkey, BuildKeyboardLParam(vkey, isKeyUp: false));
    }

    public bool TryKeyUp(string processName, Key key)
    {
        return TryKeyUp(AutomationProcessWindowTarget.From(processName), key);
    }

    public bool TryKeyUp(AutomationProcessWindowTarget processTarget, Key key)
    {
        if (!_processWindowResolver.TryResolveWindowHandle(processTarget, out var hwnd, out _, out _))
            return false;
        var vkey = KeyInterop.VirtualKeyFromKey(key);
        return PostMessage(hwnd, WmKeyUp, (nuint)vkey, BuildKeyboardLParam(vkey, isKeyUp: true));
    }

    public bool TryTapKey(string processName, Key key, int holdMilliseconds)
    {
        return TryTapKey(AutomationProcessWindowTarget.From(processName), key, holdMilliseconds);
    }

    public bool TryTapKey(AutomationProcessWindowTarget processTarget, Key key, int holdMilliseconds)
    {
        if (!TryKeyDown(processTarget, key))
            return false;
        if (holdMilliseconds > 0)
            Thread.Sleep(holdMilliseconds);
        return TryKeyUp(processTarget, key);
    }

    public bool TryMouseDown(string processName, string button, int screenX, int screenY)
    {
        return TryMouseDown(AutomationProcessWindowTarget.From(processName), button, screenX, screenY);
    }

    public bool TryMouseDown(AutomationProcessWindowTarget processTarget, string button, int screenX, int screenY)
    {
        if (!_processWindowResolver.TryResolveWindowHandle(processTarget, out var hwnd, out _, out _))
            return false;
        if (!TryBuildMouseLParam(hwnd, screenX, screenY, out var lParam))
            return false;
        var (message, wParam) = ResolveMouseMessage(button, isDown: true);
        return PostMessage(hwnd, message, wParam, lParam);
    }

    public bool TryMouseUp(string processName, string button, int screenX, int screenY)
    {
        return TryMouseUp(AutomationProcessWindowTarget.From(processName), button, screenX, screenY);
    }

    public bool TryMouseUp(AutomationProcessWindowTarget processTarget, string button, int screenX, int screenY)
    {
        if (!_processWindowResolver.TryResolveWindowHandle(processTarget, out var hwnd, out _, out _))
            return false;
        if (!TryBuildMouseLParam(hwnd, screenX, screenY, out var lParam))
            return false;
        var (message, _) = ResolveMouseMessage(button, isDown: false);
        return PostMessage(hwnd, message, 0, lParam);
    }

    public bool TryMouseClick(string processName, string button, int screenX, int screenY, int holdMilliseconds)
    {
        return TryMouseClick(AutomationProcessWindowTarget.From(processName), button, screenX, screenY, holdMilliseconds);
    }

    public bool TryMouseClick(AutomationProcessWindowTarget processTarget, string button, int screenX, int screenY, int holdMilliseconds)
    {
        if (!TryMouseDown(processTarget, button, screenX, screenY))
            return false;
        if (holdMilliseconds > 0)
            Thread.Sleep(holdMilliseconds);
        return TryMouseUp(processTarget, button, screenX, screenY);
    }

    private static bool TryBuildMouseLParam(IntPtr hwnd, int screenX, int screenY, out nuint lParam)
    {
        lParam = 0;
        if (hwnd == IntPtr.Zero)
            return false;
        var point = new POINT { X = screenX, Y = screenY };
        if (!ScreenToClient(hwnd, ref point))
            return false;
        lParam = BuildMouseLParam(point.X, point.Y);
        return true;
    }

    private static (uint Message, nuint WParam) ResolveMouseMessage(string button, bool isDown)
    {
        if (string.Equals(button, "right", StringComparison.OrdinalIgnoreCase))
            return isDown ? (WmRButtonDown, MkRButton) : (WmRButtonUp, 0);
        if (string.Equals(button, "middle", StringComparison.OrdinalIgnoreCase))
            return isDown ? (WmMButtonDown, MkMButton) : (WmMButtonUp, 0);
        return isDown ? (WmLButtonDown, MkLButton) : (WmLButtonUp, 0);
    }

    private static nuint BuildMouseLParam(int x, int y)
    {
        var packed = ((y & 0xFFFF) << 16) | (x & 0xFFFF);
        return unchecked((nuint)packed);
    }

    private static nuint BuildKeyboardLParam(int virtualKey, bool isKeyUp)
    {
        var scanCode = MapVirtualKey((uint)virtualKey, 0);
        var flags = isKeyUp ? (1u << 30) | (1u << 31) : 0u;
        var packed = 1u | (scanCode << 16) | flags;
        return (nuint)packed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, nuint wParam, nuint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
