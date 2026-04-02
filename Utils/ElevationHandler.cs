using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;

namespace GamepadMapperGUI.Utils;

public sealed class ElevationHandler : IElevationHandler
{
    private readonly IProcessTargetService _processTargetService;
    private readonly bool _isCurrentProcessElevated;
    private int? _lastElevationPromptedProcessId;

    public ElevationHandler(IProcessTargetService processTargetService)
    {
        _processTargetService = processTargetService;
        _isCurrentProcessElevated = _processTargetService.IsCurrentProcessElevated();
    }

    public bool IsBlockedByUipi(ProcessInfo target)
    {
        if (_isCurrentProcessElevated)
            return false;

        var isElevated = _processTargetService.IsProcessElevated(target.ProcessId);
        if (isElevated)
        {
            Gamepad_Mapping.App.Logger.Warning($"Target process {target.ProcessName} (PID {target.ProcessId}) is elevated, but current process is not. Input may be blocked by UIPI.");
        }
        return isElevated;
    }

    public void CheckAndPromptElevation(ProcessInfo target)
    {
        if (!IsBlockedByUipi(target))
            return;

        if (_lastElevationPromptedProcessId == target.ProcessId)
            return;

        _lastElevationPromptedProcessId = target.ProcessId;
        Gamepad_Mapping.App.Logger.Info($"Prompting for elevation due to target {target.ProcessName} (PID {target.ProcessId})");
        
        var relaunch = MessageBox.Show(
            $"The selected target '{target.ProcessName}' is running as administrator.\n\n" +
            "This mapper is not elevated, so Windows UIPI can block injected input.\n\n" +
            "Relaunch this tool as administrator now?",
            "Administrator rights required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (relaunch == MessageBoxResult.Yes)
        {
            Gamepad_Mapping.App.Logger.Info("User accepted elevation relaunch.");
            RelaunchAsAdministrator();
        }
        else
        {
            Gamepad_Mapping.App.Logger.Warning("User declined elevation relaunch. Input may be non-functional for the target.");
        }
    }

    private static void RelaunchAsAdministrator()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            Application.Current?.Shutdown();
        }
        catch (Win32Exception)
        {
            // User cancelled the UAC prompt.
        }
        catch
        {
            // Best-effort relaunch only.
        }
    }
}
