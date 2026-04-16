#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GamepadMapperGUI.Models.Core.Community;

public sealed class CommunityTemplateWorkerSubmissionPayload
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("repoOwner")]
    public string RepoOwner { get; init; } = string.Empty;

    [JsonPropertyName("repoName")]
    public string RepoName { get; init; } = string.Empty;

    [JsonPropertyName("baseBranch")]
    public string BaseBranch { get; init; } = string.Empty;

    [JsonPropertyName("catalogFolder")]
    public string CatalogFolder { get; init; } = string.Empty;

    [JsonPropertyName("gameFolderSegment")]
    public string GameFolderSegment { get; init; } = string.Empty;

    [JsonPropertyName("authorSegment")]
    public string AuthorSegment { get; init; } = string.Empty;

    [JsonPropertyName("listingDescription")]
    public string ListingDescription { get; init; } = string.Empty;

    [JsonPropertyName("profileIds")]
    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    [JsonPropertyName("files")]
    public IReadOnlyList<CommunityTemplateWorkerSubmissionFile> Files { get; init; } = [];
}
