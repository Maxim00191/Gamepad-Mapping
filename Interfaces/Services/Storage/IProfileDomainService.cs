using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Storage;

/// <summary>
/// Encapsulates domain logic for profiles, including validation and ID generation.
/// This service is decoupled from infrastructure and UI concerns.
/// </summary>
public interface IProfileDomainService
{
    IValidationResult ValidateTemplate(GameProfileTemplate template);
    string CreateUniqueProfileId(string templateGroupId, string? displayName, IEnumerable<string> existingIds);
    string EnsureUniqueId(string? preferred, IEnumerable<string?> existing, string fallbackPrefix);
    bool IsValidId(string? id);
}
