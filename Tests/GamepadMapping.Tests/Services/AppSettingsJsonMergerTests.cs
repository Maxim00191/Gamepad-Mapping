using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Storage;
using Newtonsoft.Json;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class AppSettingsJsonMergerTests
{
    [Fact]
    public void MergeToJsonString_LocalMissingNewKey_InheritsFromDefault()
    {
        const string baseline = /*lang=json,strict*/ """
            {
              "communityProfilesUploadWorkerUrl": "https://worker.example/submit",
              "defaultProfileId": "from-default"
            }
            """;

        const string overlay = /*lang=json,strict*/ """
            {
              "defaultProfileId": "from-local"
            }
            """;

        var merged = AppSettingsJsonMerger.MergeToJsonString(baseline, overlay);
        var settings = JsonConvert.DeserializeObject<AppSettings>(merged)!;

        Assert.Equal("from-local", settings.DefaultProfileId);
        Assert.Equal("https://worker.example/submit", settings.CommunityProfilesUploadWorkerUrl);
    }

    [Fact]
    public void MergeToJsonString_LocalEmptyString_InheritsFromDefault()
    {
        const string baseline = /*lang=json,strict*/ """
            {
              "communityProfilesUploadWorkerUrl": "https://worker.example/submit",
              "defaultProfileId": "from-default"
            }
            """;

        const string overlay = /*lang=json,strict*/ """
            {
              "communityProfilesUploadWorkerUrl": "",
              "defaultProfileId": "from-local"
            }
            """;

        var merged = AppSettingsJsonMerger.MergeToJsonString(baseline, overlay);
        var settings = JsonConvert.DeserializeObject<AppSettings>(merged)!;

        Assert.Equal("from-local", settings.DefaultProfileId);
        Assert.Equal("https://worker.example/submit", settings.CommunityProfilesUploadWorkerUrl);
    }

    [Fact]
    public void MergeToJsonString_LocalOverridesDefaultUrl()
    {
        const string baseline = /*lang=json,strict*/ """
            {
              "communityProfilesUploadWorkerUrl": "https://worker.example/submit"
            }
            """;

        const string overlay = /*lang=json,strict*/ """
            {
              "communityProfilesUploadWorkerUrl": "https://override.example/submit"
            }
            """;

        var merged = AppSettingsJsonMerger.MergeToJsonString(baseline, overlay);
        var settings = JsonConvert.DeserializeObject<AppSettings>(merged)!;

        Assert.Equal("https://override.example/submit", settings.CommunityProfilesUploadWorkerUrl);
    }

    [Fact]
    public void MergeToJsonString_NestedObjectsCombineProperties()
    {
        const string baseline = /*lang=json,strict*/ """
            {
              "updateInstallPolicy": {
                "preservePaths": [ "Assets/Profiles/templates" ],
                "removeOrphanFiles": true
              }
            }
            """;

        const string overlay = /*lang=json,strict*/ """
            {
              "updateInstallPolicy": {
                "removeOrphanFiles": false
              }
            }
            """;

        var merged = AppSettingsJsonMerger.MergeToJsonString(baseline, overlay);
        var settings = JsonConvert.DeserializeObject<AppSettings>(merged)!;

        Assert.NotNull(settings.UpdateInstallPolicy);
        Assert.False(settings.UpdateInstallPolicy.RemoveOrphanFiles);
        Assert.Single(settings.UpdateInstallPolicy.PreservePaths!);
        Assert.Equal("Assets/Profiles/templates", settings.UpdateInstallPolicy.PreservePaths![0]);
    }
}
