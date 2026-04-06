using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;

namespace GamepadMapperGUI.Services;

public class ProfileValidator : IValidator<GameProfileTemplate>
{
    public IValidationResult Validate(GameProfileTemplate profile)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.ProfileId))
            errors.Add("Profile ID is required.");

        if (!string.IsNullOrWhiteSpace(profile.TemplateGroupId) && !ProfileService.IsValidId(profile.TemplateGroupId))
            errors.Add("Template Group ID (when set) contains invalid characters.");

        // Validate Keyboard Actions
        var actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (profile.KeyboardActions != null)
        {
            foreach (var action in profile.KeyboardActions)
            {
                if (string.IsNullOrWhiteSpace(action.Id))
                    errors.Add("Keyboard Action ID cannot be empty.");
                else if (!actionIds.Add(action.Id))
                    errors.Add($"Duplicate Keyboard Action ID: {action.Id}");
                
                if (!action.HasOutput)
                    warnings.Add($"Action '{action.Id}' has no output (KeyboardKey, TemplateToggle or RadialMenu).");
            }
        }

        // Validate Radial Menus
        var radialMenuIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (profile.RadialMenus != null)
        {
            foreach (var rm in profile.RadialMenus)
            {
                if (string.IsNullOrWhiteSpace(rm.Id))
                    errors.Add("Radial Menu ID cannot be empty.");
                else if (!radialMenuIds.Add(rm.Id))
                    errors.Add($"Duplicate Radial Menu ID: {rm.Id}");

                if (rm.Items == null || rm.Items.Count == 0)
                    warnings.Add($"Radial Menu '{rm.Id}' has no items.");
                else
                {
                    foreach (var item in rm.Items)
                    {
                        if (!string.IsNullOrWhiteSpace(item.ActionId) && !actionIds.Contains(item.ActionId))
                            errors.Add($"Radial Menu '{rm.Id}' references unknown Action ID: {item.ActionId}");
                    }
                }
            }
        }

        // Validate Mappings
        if (profile.Mappings != null)
        {
            foreach (var mapping in profile.Mappings)
            {
                if (mapping.From == null || string.IsNullOrWhiteSpace(mapping.From.Value))
                    errors.Add("Mapping has no input button/chord.");

                if (!string.IsNullOrWhiteSpace(mapping.ActionId))
                {
                    if (!actionIds.Contains(mapping.ActionId))
                        errors.Add($"Mapping references unknown Action ID: {mapping.ActionId}");
                }
                else if (mapping.ActionType == MappingActionType.Keyboard && string.IsNullOrWhiteSpace(mapping.KeyboardKey))
                {
                    errors.Add($"Mapping for '{mapping.From?.Value}' has no output key.");
                }

                if (mapping.RadialMenu != null && !radialMenuIds.Contains(mapping.RadialMenu.RadialMenuId))
                    errors.Add($"Mapping for '{mapping.From?.Value}' references unknown Radial Menu: {mapping.RadialMenu.RadialMenuId}");
            }
        }

        return new ValidationResult(errors, warnings);
    }
}
