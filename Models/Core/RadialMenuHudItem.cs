namespace GamepadMapperGUI.Models;

/// <summary>Display payload for the radial menu HUD (decoupled from WPF ViewModels).</summary>
public sealed class RadialMenuHudItem
{
    public required string ActionId { get; init; }
    public required string DisplayName { get; init; }
    public string? Icon { get; init; }
}
