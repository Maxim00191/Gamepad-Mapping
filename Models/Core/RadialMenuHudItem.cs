namespace GamepadMapperGUI.Models;

/// <summary>Display payload for the radial menu HUD (decoupled from WPF ViewModels).</summary>
public sealed class RadialMenuHudItem
{
    public required string ActionId { get; init; }

    /// <summary>Catalog / fallback label (description or action id).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Raw keyboard token from <see cref="KeyboardActionDefinition.KeyboardKey"/> for HUD (e.g. Space, E).</summary>
    public string KeyboardKeyLabel { get; init; } = string.Empty;

    public string? Icon { get; init; }
}
