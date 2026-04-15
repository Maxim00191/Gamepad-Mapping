using System;

namespace GamepadMapperGUI.Models.State;

public sealed record CommunityUploadDialogMemory(
    string BundleFingerprint,
    CommunityUploadDialogDraft Draft)
{
    public bool Matches(string bundleFingerprint)
    {
        return string.Equals(
            BundleFingerprint,
            bundleFingerprint ?? string.Empty,
            StringComparison.Ordinal);
    }
}
