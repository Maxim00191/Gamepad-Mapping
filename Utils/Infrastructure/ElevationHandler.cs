using System;
using System.Windows;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;

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

    public bool CheckAndPromptElevation(ProcessInfo target)
    {
        if (!IsBlockedByUipi(target))
            return false;

        if (_lastElevationPromptedProcessId == target.ProcessId)
            return false;

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
            return ElevationApplicationRelaunch.TryRelaunchElevatedAndShutdownCurrentApplication();
        }

        Gamepad_Mapping.App.Logger.Warning("User declined elevation relaunch. Input may be non-functional for the target.");
        return false;
    }

}


