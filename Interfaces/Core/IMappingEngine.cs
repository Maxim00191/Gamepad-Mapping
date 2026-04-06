using System;
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IMappingEngine : IDisposable
{
    /// <summary>Names from profile <c>comboLeadButtons</c>; null = infer from mappings.</summary>
    void SetComboLeadButtonsFromTemplate(IReadOnlyList<string>? comboLeadButtonNames);

    /// <param name="canDispatchMappedOutput">Foreground/targeting gate evaluated once for this frame; must match repeated checks within the same <see cref="InputFrame"/>.</param>
    InputFrameProcessingResult ProcessInputFrame(InputFrame frame, IReadOnlyList<MappingEntry> mappingsSnapshot, bool canDispatchMappedOutput = true);
    void ForceReleaseAllOutputs();
    void ForceReleaseAnalogOutputs();

    /// <summary>
    /// Re-evaluates combo HUD state (e.g. after UI-only transitions such as template-switch HUD timeout).
    /// </summary>
    void RefreshComboHud();

    /// <summary>
    /// Clears combo HUD de-duplication state so the next sync can push content again (e.g. after template-switch HUD).
    /// </summary>
    void InvalidateComboHudPresentation();

    void SetRadialMenuDefinitions(List<RadialMenuDefinition>? radialMenus, List<KeyboardActionDefinition>? keyboardActions, IKeyboardActionCatalog? catalog = null);

    /// <summary>
    /// Waits until all queued background outputs have been dispatched.
    /// Used primarily in tests to avoid flaky assertions.
    /// </summary>
    Task WaitForIdleAsync();
}
