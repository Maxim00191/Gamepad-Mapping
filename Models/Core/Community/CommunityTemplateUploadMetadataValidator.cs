#nullable enable

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GamepadMapperGUI.Models.Core.Community;

public static partial class CommunityTemplateUploadMetadataValidator
{
    /// <summary>
    /// Letters (any language), numbers, space, hyphen, underscore, apostrophe — no other symbols.
    /// </summary>
    [GeneratedRegex(@"^[\p{L}\p{M}\p{Nd} _\-']+$", RegexOptions.CultureInvariant)]
    private static partial Regex AuthorNameAllowedCharacters();

    public static bool IsAuthorNameCharactersAllowed(string trimmedAuthorDisplayName)
    {
        if (trimmedAuthorDisplayName.Length == 0)
            return false;

        return AuthorNameAllowedCharacters().IsMatch(trimmedAuthorDisplayName);
    }

    public static bool IsMetadataValidForSubmission(
        string authorTrimmed,
        string listingDescriptionTrimmed,
        out string? invariantEnglishError)
    {
        invariantEnglishError = null;

        if (authorTrimmed.Length > CommunityTemplateUploadConstraints.MaxAuthorDisplayNameLength)
        {
            invariantEnglishError = string.Format(
                CultureInfo.InvariantCulture,
                "Author name must be {0} characters or fewer.",
                CommunityTemplateUploadConstraints.MaxAuthorDisplayNameLength);
            return false;
        }

        if (authorTrimmed.Length > 0
            && !IsAuthorNameCharactersAllowed(authorTrimmed))
        {
            invariantEnglishError =
                "Author name can only include letters, numbers, spaces, hyphen (-), underscore (_), and apostrophe (').";
            return false;
        }

        if (listingDescriptionTrimmed.Length > CommunityTemplateUploadConstraints.MaxListingDescriptionCharacters)
        {
            invariantEnglishError = string.Format(
                CultureInfo.InvariantCulture,
                "Listing description must be {0} characters or fewer.",
                CommunityTemplateUploadConstraints.MaxListingDescriptionCharacters);
            return false;
        }

        return true;
    }
}
