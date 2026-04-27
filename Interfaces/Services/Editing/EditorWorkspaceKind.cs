namespace GamepadMapperGUI.Interfaces.Services.Editing;

/// <summary>Identifies a profile template editing surface (one per main workspace tab with rule editing).</summary>
public enum EditorWorkspaceKind
{
    None = 0,
    Mappings = 1,
    Visual = 2,
    KeyboardActions = 3,
    RadialMenus = 4
}
