using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services;

public sealed class CommunityUploadBundleFingerprintTests
{
    [Fact]
    public void Compute_SameBundleDifferentOrder_ReturnsSameFingerprint()
    {
        var first = new[]
        {
            new CommunityTemplateBundleEntry("b.json", new GameProfileTemplate { ProfileId = "b", DisplayName = "B" }),
            new CommunityTemplateBundleEntry("a.json", new GameProfileTemplate { ProfileId = "a", DisplayName = "A" })
        };
        var second = new[]
        {
            new CommunityTemplateBundleEntry("a.json", new GameProfileTemplate { ProfileId = "a", DisplayName = "A" }),
            new CommunityTemplateBundleEntry("b.json", new GameProfileTemplate { ProfileId = "b", DisplayName = "B" })
        };

        var hash1 = CommunityUploadBundleFingerprint.Compute(first);
        var hash2 = CommunityUploadBundleFingerprint.Compute(second);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_ChangedTemplateContent_ReturnsDifferentFingerprint()
    {
        var original = new[]
        {
            new CommunityTemplateBundleEntry("a.json", new GameProfileTemplate { ProfileId = "a", DisplayName = "A" })
        };
        var changed = new[]
        {
            new CommunityTemplateBundleEntry("a.json", new GameProfileTemplate { ProfileId = "a", DisplayName = "A changed" })
        };

        var hash1 = CommunityUploadBundleFingerprint.Compute(original);
        var hash2 = CommunityUploadBundleFingerprint.Compute(changed);

        Assert.NotEqual(hash1, hash2);
    }
}
