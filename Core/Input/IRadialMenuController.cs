using System;
using System.Collections.Generic;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal interface IRadialMenuController : IActionSession
{
    RadialMenuDefinition? ActiveRadial { get; }
    int CurrentSelectedIndex { get; set; }
    
    void SetDefinitions(List<RadialMenuDefinition>? radialMenus, List<KeyboardActionDefinition>? keyboardActions, IKeyboardActionCatalog? catalog = null);

    void SetActionExecutor(IKeyboardActionExecutor executor);

    public void UpdateSelection(Vector2 stick, float engagementThreshold, RadialMenuConfirmMode confirmMode);
    
    bool TryOpen(MappingEntry mapping, string sourceToken, out string? errorStatus);
    bool TryClose(string radialMenuId, string sourceToken, bool dispatchSelection, bool suppressChord = false);
    void ForceReset();
}

