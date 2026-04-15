using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services;

public sealed class CommunityTemplateUploadPathConflictCheckerTests
{
    [Fact]
    public void FindConflictingRelativePaths_DetectsPublishedCollision()
    {
        var index = new List<CommunityTemplateInfo>
        {
            new()
            {
                CatalogFolder = "MyGame/Alice",
                FileName = "profile-a.json",
            },
        };
        var published = CommunityTemplateUploadPathConflictChecker.BuildPublishedPathSet(index);
        var templates = new[]
        {
            new GameProfileTemplate { ProfileId = "profile-a" },
        };

        var conflicts = CommunityTemplateUploadPathConflictChecker.FindConflictingRelativePaths(
            templates,
            "MyGame/Alice",
            published);

        Assert.Single(conflicts);
        Assert.Equal("MyGame/Alice/profile-a.json", conflicts[0]);
    }

    [Fact]
    public void FindConflictingRelativePaths_DetectsDuplicateProfileIdInSameSubmission()
    {
        var published = CommunityTemplateUploadPathConflictChecker.BuildPublishedPathSet([]);
        var templates = new[]
        {
            new GameProfileTemplate { ProfileId = "same-id" },
            new GameProfileTemplate { ProfileId = "same-id" },
        };

        var conflicts = CommunityTemplateUploadPathConflictChecker.FindConflictingRelativePaths(
            templates,
            "G/A",
            published);

        Assert.Single(conflicts);
        Assert.Equal("G/A/same-id.json", conflicts[0]);
    }

    [Fact]
    public void BuildPublishedPathSet_NormalizesBackslashInCatalogFolder()
    {
        var index = new List<CommunityTemplateInfo>
        {
            new() { CatalogFolder = @"X\Y", FileName = "z.json" },
        };
        var set = CommunityTemplateUploadPathConflictChecker.BuildPublishedPathSet(index);
        Assert.Contains("X/Y/z.json", set);
    }
}
