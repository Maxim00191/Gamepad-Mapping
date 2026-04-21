using System.Windows;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Coordinates UAC relaunch with shutting down the current WPF instance. Callers must stop further work when this returns true.
/// </summary>
public static class ElevationApplicationRelaunch
{
    /// <summary>
    /// Starts an elevated copy of this process and requests application shutdown. Returns false if UAC was cancelled or startup failed.
    /// </summary>
    /// <remarks>
    /// WPF continues executing the current call stack after <see cref="Application.Shutdown"/> until the method returns;
    /// callers must return immediately when this method returns true (e.g. skip constructing the main window during <c>OnStartup</c>).
    /// </remarks>
    public static bool TryRelaunchElevatedAndShutdownCurrentApplication()
    {
        if (!Gamepad_Mapping.Utils.ElevationProcessRelaunch.TryRelaunchCurrentProcessAsAdministrator())
            return false;

        Application.Current?.Shutdown();
        return true;
    }
}
