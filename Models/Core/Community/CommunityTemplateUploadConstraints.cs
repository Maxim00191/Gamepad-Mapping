namespace GamepadMapperGUI.Models.Core.Community;

public static class CommunityTemplateUploadConstraints
{
    public const int MaxFilesPerSubmission = 8;
    public const string RequiredFileExtension = ".json";
    public const int MaxTemplateFileBytes = 1 * 1024 * 1024;

    /// <summary>Maximum length for the trimmed author display name shown in the upload dialog.</summary>
    public const int MaxAuthorDisplayNameLength = 30;

    /// <summary>Maximum length for the trimmed community listing / summary text (multi-line).</summary>
    public const int MaxListingDescriptionCharacters = 1500;
}
