#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Input;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationNodeInputModeResolver : IAutomationNodeInputModeResolver
{
    private readonly (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) _defaultPair;
    private readonly Dictionary<string, (IKeyboardEmulator Keyboard, IMouseEmulator Mouse)> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public AutomationNodeInputModeResolver(IKeyboardEmulator keyboard, IMouseEmulator mouse)
    {
        _defaultPair = (keyboard, mouse);
    }

    public (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) Resolve(string? requestedModeId)
    {
        var normalizedId = AutomationInputModeCatalog.NormalizeModeId(requestedModeId);
        if (normalizedId.Length == 0)
            return _defaultPair;

        if (_cache.TryGetValue(normalizedId, out var pair))
            return pair;

        pair = InputEmulationServices.CreatePair(normalizedId);
        _cache[normalizedId] = pair;
        return pair;
    }
}
