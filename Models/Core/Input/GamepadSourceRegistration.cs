namespace GamepadMapperGUI.Models.Core.Input;

public sealed record GamepadSourceRegistration(
    string Id,
    string DisplayNameLocalizationKey,
    bool IsImplemented);
