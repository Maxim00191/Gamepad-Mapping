namespace GamepadMapperGUI.Models;

/// <summary>How a radial menu selection is committed after the HUD opens.</summary>
public enum RadialMenuConfirmMode
{
    /// <summary>Release the chord that opened the menu (default). May jerk camera if stick is still deflected.</summary>
    ReleaseGuideKey = 0,

    /// <summary>Return the selection stick to neutral after highlighting a sector; then chord input is ignored until physical release.</summary>
    ReturnStickToCenter = 1
}
