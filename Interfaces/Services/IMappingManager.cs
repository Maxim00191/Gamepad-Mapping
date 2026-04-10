using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services;

/// <summary>
/// Manages the current mapping state, profile loading, and engine synchronization.
/// </summary>
public interface IMappingManager : IDisposable
{
    ObservableCollection<MappingEntry> Mappings { get; }
    ObservableCollection<KeyboardActionDefinition> KeyboardActions { get; }
    ObservableCollection<RadialMenuDefinition> RadialMenus { get; }
    
    MappingEntry? SelectedMapping { get; set; }
    int MappingCount { get; }
    
    event EventHandler? MappingsChanged;
    event Action<InputFrame, InputFrameProcessingResult>? OnInputProcessed;

    void LoadTemplate(GameProfileTemplate template);
    void ProcessInputFrame(InputFrame frame, bool allowOutput);
    void RefreshEngineDefinitions();
    void ForceReleaseOutputs();

    /// <summary>Replaces the mapping engine (e.g. after changing input API). Releases outputs on the old engine first.</summary>
    void ReplaceEngine(IMappingEngine newEngine, IReadOnlyList<string>? comboLeadButtons);
}
