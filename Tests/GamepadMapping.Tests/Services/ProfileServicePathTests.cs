using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapping.Tests.Mocks;
using Xunit;
using System;

namespace GamepadMapping.Tests.Services;

public class ProfileServicePathTests
{
    [Theory]
    [InlineData("valid-id")]
    [InlineData("valid.id")]
    [InlineData("valid_id")]
    [InlineData("123-abc")]
    public void EnsureValidTemplateGroupId_ValidInputs_ReturnsNormalized(string input)
    {
        var result = ProfileService.EnsureValidTemplateGroupId(input);
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("invalid/path")]
    [InlineData(".starts-with-dot")]
    public void EnsureValidTemplateGroupId_InvalidInputs_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => ProfileService.EnsureValidTemplateGroupId(input!));
    }

    [Theory]
    [InlineData("Game Name", "mygame__game-name")]
    [InlineData("Game! Name@ #123", "mygame__game-name-123")]
    [InlineData("   Spaces   ", "mygame__spaces")]
    [InlineData("---Dashes---", "mygame__dashes")]
    [InlineData("Mixed_Case.Dots", "mygame__mixed-case-dots")]
    public void CreateUniqueProfileId_GeneratesSafeSlug(string displayName, string expectedId)
    {
        var mockFs = new MockFileSystem();
        var mockPath = new MockPathProvider();
        var service = new ProfileService(new MockSettingsService(), null, mockFs, mockPath);
        var templateGroupId = "mygame";
        
        var profileId = service.CreateUniqueProfileId(templateGroupId, displayName);
        
        Assert.Equal(expectedId, profileId);
    }

    [Fact]
    public void CreateUniqueProfileId_HandlesConflicts_Deterministically()
    {
        var mockFs = new MockFileSystem();
        var mockPath = new MockPathProvider();
        var templateGroupId = "mygame";
        var displayName = "Conflict";
        var expectedBaseId = "mygame__conflict";

        var service = new ProfileService(new MockSettingsService(), null, mockFs, mockPath);

        // CreateUniqueProfileId resolves conflicts against in-memory AvailableTemplates, not raw disk files.
        service.AvailableTemplates.Add(new TemplateOption { ProfileId = expectedBaseId, TemplateGroupId = templateGroupId });

        var profileId = service.CreateUniqueProfileId(templateGroupId, displayName);
        Assert.Equal($"{expectedBaseId}-2", profileId);

        service.AvailableTemplates.Add(new TemplateOption { ProfileId = $"{expectedBaseId}-2", TemplateGroupId = templateGroupId });

        var profileId3 = service.CreateUniqueProfileId(templateGroupId, displayName);
        Assert.Equal($"{expectedBaseId}-3", profileId3);
    }

    private class MockSettingsService : ISettingsService
    {
        public AppSettings LoadSettings() => new();
        public void SaveSettings(AppSettings settings) { }
    }
}

