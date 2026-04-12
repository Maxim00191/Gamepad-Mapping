namespace GamepadMapperGUI.Models.Core;

public record GitHubRepositoryContentRequest(
    string Owner,
    string Repository,
    string Branch,
    string RelativePath);
