namespace GamepadMapperGUI.Models.Core.Community;

public static class CommunityTemplateUploadConstraints
{
    public const int MaxFilesPerSubmission = 8;
    public const string RequiredFileExtension = ".json";
    public const int MaxTemplateFileBytes = 1 * 1024 * 1024;
}
