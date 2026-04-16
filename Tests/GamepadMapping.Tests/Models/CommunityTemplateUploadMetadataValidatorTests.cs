using GamepadMapperGUI.Models.Core.Community;
using Xunit;

namespace GamepadMapping.Tests.Models;

public sealed class CommunityTemplateUploadMetadataValidatorTests
{
    [Fact]
    public void IsAuthorNameCharactersAllowed_AllowsLettersDigitsSpaceHyphenUnderscoreApostrophe()
    {
        Assert.True(CommunityTemplateUploadMetadataValidator.IsAuthorNameCharactersAllowed("Player_One"));
        Assert.True(CommunityTemplateUploadMetadataValidator.IsAuthorNameCharactersAllowed("José 99"));
        Assert.True(CommunityTemplateUploadMetadataValidator.IsAuthorNameCharactersAllowed("O'Brien"));
    }

    [Fact]
    public void IsAuthorNameCharactersAllowed_RejectsEmptyAndDisallowedSymbols()
    {
        Assert.False(CommunityTemplateUploadMetadataValidator.IsAuthorNameCharactersAllowed(""));
        Assert.False(CommunityTemplateUploadMetadataValidator.IsAuthorNameCharactersAllowed("a@b"));
        Assert.False(CommunityTemplateUploadMetadataValidator.IsAuthorNameCharactersAllowed("name."));
    }

    [Fact]
    public void IsMetadataValidForSubmission_RejectsAuthorTooLong()
    {
        var author = new string('a', CommunityTemplateUploadConstraints.MaxAuthorDisplayNameLength + 1);
        var ok = CommunityTemplateUploadMetadataValidator.IsMetadataValidForSubmission(author, "ok", out var err);
        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("30", err, StringComparison.Ordinal);
    }

    [Fact]
    public void IsMetadataValidForSubmission_RejectsListingTooLong()
    {
        var listing = new string('x', CommunityTemplateUploadConstraints.MaxListingDescriptionCharacters + 1);
        var ok = CommunityTemplateUploadMetadataValidator.IsMetadataValidForSubmission("Author", listing, out var err);
        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("1500", err, StringComparison.Ordinal);
    }
}
