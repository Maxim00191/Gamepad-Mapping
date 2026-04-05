using System;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Actions;

internal sealed class TemplateToggleAction(
    MappingEntry mapping,
    Func<bool> canDispatch,
    Action<string> setMappedOutput,
    Action<string> setMappingStatus,
    Action<string>? requestTemplateSwitch) : IExecutableAction
{
    public bool Execute(TriggerMoment trigger, string sourceToken, out string? errorStatus)
    {
        errorStatus = null;
        if (mapping.TemplateToggle is not { } tt)
            return false;

        if (trigger == TriggerMoment.Released)
            return true;

        if (!canDispatch())
            return true;

        var profileId = tt.AlternateProfileId?.Trim() ?? string.Empty;
        if (profileId.Length == 0)
        {
            errorStatus = "Toggle profile: missing alternateProfileId.";
            return true;
        }

        var label = $"Toggle profile → {profileId}";
        setMappedOutput($"{label} ({trigger})");
        setMappingStatus($"Queued: {sourceToken} ({trigger}) -> {label}");
        requestTemplateSwitch?.Invoke(profileId);
        return true;
    }

    public bool TryExecuteDeferredSoloRelease(string sourceToken, out string? errorStatus)
    {
        return Execute(TriggerMoment.Tap, sourceToken, out errorStatus);
    }

    public bool RequiresDeferralOnPress => true;
}
