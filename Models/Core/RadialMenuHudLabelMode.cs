namespace GamepadMapperGUI.Models;

/// <summary>How radial menu item labels combine description vs bound keyboard key.</summary>
public enum RadialMenuHudLabelMode
{
    /// <summary>Show description and keyboard key (key as secondary line).</summary>
    Both = 0,

    /// <summary>Show catalog description only.</summary>
    DescriptionOnly = 1,

    /// <summary>Show bound keyboard key token only (falls back to description if missing).</summary>
    KeyboardKeyOnly = 2
}
