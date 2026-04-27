using System.Collections.ObjectModel;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using Gamepad_Mapping.ViewModels;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Represents the state of the workspace required by services.
/// Decouples services from the full MainViewModel.
/// </summary>
public interface IWorkspaceState
{
    GameProfileTemplate? GetWorkspaceTemplateSnapshot();

    TemplateOption? SelectedTemplate { get; }
    MappingEntry? SelectedMapping { get; set; }
    KeyboardActionDefinition? SelectedKeyboardAction { get; set; }
    RadialMenuDefinition? SelectedRadialMenu { get; set; }
    
    ObservableCollection<MappingEntry> Mappings { get; }
    ObservableCollection<KeyboardActionDefinition> KeyboardActions { get; }
    ObservableCollection<RadialMenuDefinition> RadialMenus { get; }
    
    bool IsCreatingNewMapping { get; set; }

    void RefreshMappingEngineDefinitions();
    void RefreshAfterRulePastedFromClipboard();
    void NotifyConfigurationChanged(ProfileRuleClipboardKind kind);
    bool TryBuildMappingFromEditorFields(out MappingEntry entry, out string? messageKey);
    bool TryApplyAnalogThreshold(MappingEntry entry, string thresholdText);
    void ApplyDescriptionPairToMapping(MappingEntry entry, string primary, string secondary);
    
    // Catalog specific
    void SyncCatalogOutputKindFromSelection();
    void PullKeyboardCatalogDescriptionPair(out string primary, out string secondary);
    void PushKeyboardCatalogDescriptionPair(string primary, string secondary);
    void PullRadialMenuDisplayNamePair(out string primary, out string secondary);
    void PushRadialMenuDisplayNamePair(string primary, string secondary);
}
