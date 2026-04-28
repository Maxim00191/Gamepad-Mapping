#nullable enable

using System.Runtime.InteropServices;

namespace GamepadMapperGUI.Services.Automation;

internal sealed class AutomationDpiAwarenessScope : IDisposable
{
    private static readonly IntPtr PerMonitorAware = new(-3);
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);
    private static readonly AutomationDpiAwarenessScope Empty = new(IntPtr.Zero);

    private readonly IntPtr _previousThreadContext;

    private AutomationDpiAwarenessScope(IntPtr previousThreadContext)
    {
        _previousThreadContext = previousThreadContext;
    }

    public static IDisposable EnterPerMonitorAware()
    {
        var previous = TrySetThreadDpiAwarenessContext(PerMonitorAwareV2);
        if (previous != IntPtr.Zero)
            return new AutomationDpiAwarenessScope(previous);

        previous = TrySetThreadDpiAwarenessContext(PerMonitorAware);
        return previous == IntPtr.Zero ? Empty : new AutomationDpiAwarenessScope(previous);
    }

    public static void TrySetProcessPerMonitorAware()
    {
        if (TrySetProcessDpiAwarenessContext(PerMonitorAwareV2))
            return;

        TrySetProcessDpiAwarenessContext(PerMonitorAware);
    }

    public void Dispose()
    {
        if (_previousThreadContext != IntPtr.Zero)
            TrySetThreadDpiAwarenessContext(_previousThreadContext);
    }

    private static bool TrySetProcessDpiAwarenessContext(IntPtr dpiContext)
    {
        try
        {
            return SetProcessDpiAwarenessContext(dpiContext);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (SEHException)
        {
            return false;
        }
    }

    private static IntPtr TrySetThreadDpiAwarenessContext(IntPtr dpiContext)
    {
        try
        {
            return SetThreadDpiAwarenessContext(dpiContext);
        }
        catch (DllNotFoundException)
        {
            return IntPtr.Zero;
        }
        catch (EntryPointNotFoundException)
        {
            return IntPtr.Zero;
        }
        catch (SEHException)
        {
            return IntPtr.Zero;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
}
