using System.Collections.Generic;

namespace GamepadMapperGUI.Models.State;

public sealed record CommunityUploadDialogDraft(
    string GameFolderName,
    string AuthorName,
    string ListingDescription,
    IReadOnlyList<string> IncludedStorageKeys);
