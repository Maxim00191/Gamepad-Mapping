using System;
using System.Runtime.InteropServices;
using GamepadMapperGUI.Interfaces.Core;

namespace GamepadMapperGUI.Core;

public sealed class MouseEmulator : IMouseEmulator
{
    private const uint InputMouse = 0;
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfRightdown = 0x0008;
    private const uint MouseeventfRightup = 0x0010;
    private const uint MouseeventfMiddledown = 0x0020;
    private const uint MouseeventfMiddleup = 0x0040;
    private const uint MouseeventfXdown = 0x0080;
    private const uint MouseeventfXup = 0x0100;
    private const uint MouseeventfMove = 0x0001;
    private const uint MouseeventfWheel = 0x0800;
    private const uint Xbutton1 = 0x0001;
    private const uint Xbutton2 = 0x0002;
    private const int WheelDelta = 120;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public void LeftDown() => SendMouseInput(MouseeventfLeftdown);
    public void LeftUp() => SendMouseInput(MouseeventfLeftup);
    public void LeftClick()
    {
        LeftDown();
        LeftUp();
    }

    public void RightDown() => SendMouseInput(MouseeventfRightdown);
    public void RightUp() => SendMouseInput(MouseeventfRightup);
    public void RightClick()
    {
        RightDown();
        RightUp();
    }

    public void MiddleDown() => SendMouseInput(MouseeventfMiddledown);
    public void MiddleUp() => SendMouseInput(MouseeventfMiddleup);
    public void MiddleClick()
    {
        MiddleDown();
        MiddleUp();
    }

    public void X1Down() => SendMouseInput(MouseeventfXdown, Xbutton1);
    public void X1Up() => SendMouseInput(MouseeventfXup, Xbutton1);
    public void X1Click()
    {
        X1Down();
        X1Up();
    }

    public void X2Down() => SendMouseInput(MouseeventfXdown, Xbutton2);
    public void X2Up() => SendMouseInput(MouseeventfXup, Xbutton2);
    public void X2Click()
    {
        X2Down();
        X2Up();
    }

    public void WheelUp() => SendMouseInput(MouseeventfWheel, unchecked((uint)WheelDelta));
    public void WheelDown() => SendMouseInput(MouseeventfWheel, unchecked((uint)-WheelDelta));
    public void MoveBy(int deltaX, int deltaY) => SendMouseInput(MouseeventfMove, 0, deltaX, deltaY);

    private static void SendMouseInput(uint flags, uint mouseData = 0, int dx = 0, int dy = 0)
    {
        var input = new INPUT
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = mouseData,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }
}
