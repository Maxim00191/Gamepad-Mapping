using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapping.Tests.Mocks;
using Xunit;
using System.IO;

namespace GamepadMapping.Tests.Services;

public class SettingsServiceTests
{
    [Fact]
    public void LoadSettings_MalformedJson_ReturnsDefaultSettings()
    {
        var mockFs = new MockFileSystem();
        var mockPath = new MockPathProvider();
        var service = new SettingsService(mockFs, mockPath);
        
        var root = mockPath.GetContentRoot();
        var localPath = Path.Combine(root, "Assets", "Config", "local_settings.json");
        
        mockFs.WriteAllText(localPath, "{ \"invalid\": json }", System.Text.Encoding.UTF8);
        
        var settings = service.LoadSettingsInternal();
        
        Assert.NotNull(settings);
        Assert.Equal(new AppSettings().DefaultProfileId, settings.DefaultProfileId);
    }

    [Fact]
    public void LoadSettings_LocalWithoutNewKey_InheritsFromDefaultSettingsFile()
    {
        var mockFs = new MockFileSystem();
        var mockPath = new MockPathProvider();
        var service = new SettingsService(mockFs, mockPath);
        var root = mockPath.GetContentRoot();
        var configDir = Path.Combine(root, "Assets", "Config");
        mockFs.CreateDirectory(configDir);
        var defaultPath = Path.Combine(configDir, "default_settings.json");
        var localPath = Path.Combine(configDir, "local_settings.json");

        mockFs.WriteAllText(
            defaultPath,
            /*lang=json,strict*/ """
            {
              "communityProfilesUploadWorkerUrl": "https://worker.example/submit",
              "defaultProfileId": "from-shipped-default"
            }
            """,
            System.Text.Encoding.UTF8);

        mockFs.WriteAllText(
            localPath,
            /*lang=json,strict*/ """
            {
              "defaultProfileId": "from-user-local"
            }
            """,
            System.Text.Encoding.UTF8);

        var settings = service.LoadSettingsInternal();

        Assert.Equal("from-user-local", settings.DefaultProfileId);
        Assert.Equal("https://worker.example/submit", settings.CommunityProfilesUploadWorkerUrl);

        // Updater refreshes default_settings.json but not local_settings.json; load persists merged keys into local.
        Assert.True(mockFs.Files.TryGetValue(localPath, out var persistedLocal));
        Assert.Contains("communityProfilesUploadWorkerUrl", persistedLocal, StringComparison.Ordinal);
        Assert.Contains("https://worker.example/submit", persistedLocal, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadSettings_LocalEmptyStringForWorkerUrl_InheritsFromDefaultAndPersists()
    {
        var mockFs = new MockFileSystem();
        var mockPath = new MockPathProvider();
        var service = new SettingsService(mockFs, mockPath);
        var root = mockPath.GetContentRoot();
        var configDir = Path.Combine(root, "Assets", "Config");
        mockFs.CreateDirectory(configDir);
        var defaultPath = Path.Combine(configDir, "default_settings.json");
        var localPath = Path.Combine(configDir, "local_settings.json");

        mockFs.WriteAllText(
            defaultPath,
            /*lang=json,strict*/ """
            {
              "communityProfilesUploadWorkerUrl": "https://worker.example/submit",
              "defaultProfileId": "from-shipped-default"
            }
            """,
            System.Text.Encoding.UTF8);

        mockFs.WriteAllText(
            localPath,
            /*lang=json,strict*/ """
            {
              "communityProfilesUploadWorkerUrl": "",
              "defaultProfileId": "from-user-local"
            }
            """,
            System.Text.Encoding.UTF8);

        var settings = service.LoadSettingsInternal();

        Assert.Equal("https://worker.example/submit", settings.CommunityProfilesUploadWorkerUrl);
        Assert.True(mockFs.Files.TryGetValue(localPath, out var persistedLocal));
        Assert.Contains("https://worker.example/submit", persistedLocal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.1f, 0.5f, 0.0f, 0.5f)]
    [InlineData(0.99f, 1.0f, 0.98f, 1.0f)]
    [InlineData(0.5f, 0.4f, 0.5f, 1.0f)]
    [InlineData(0.5f, 0.51f, 0.5f, 1.0f)]
    [InlineData(0.5f, 0.53f, 0.5f, 0.53f)]
    public void NormalizeTriggerDeadzones_CorrectsInvalidValues(float inL, float outL, float expectedInL, float expectedOutL)
    {
        var settings = new AppSettings
        {
            LeftTriggerInnerDeadzone = inL,
            LeftTriggerOuterDeadzone = outL,
            RightTriggerInnerDeadzone = inL,
            RightTriggerOuterDeadzone = outL
        };

        SettingsService.NormalizeTriggerDeadzones(settings);

        Assert.Equal(expectedInL, settings.LeftTriggerInnerDeadzone, 3);
        Assert.Equal(expectedOutL, settings.LeftTriggerOuterDeadzone, 3);
    }

    [Fact]
    public void NewAppSettings_DefaultOverlayPrimaryLabel_IsActionAndPhysical()
    {
        var settings = new AppSettings();

        Assert.Equal(
            ControllerMappingOverlayLabelModeParser.DefaultSettingValue,
            settings.ControllerMappingOverlayPrimaryLabel);
    }
}

