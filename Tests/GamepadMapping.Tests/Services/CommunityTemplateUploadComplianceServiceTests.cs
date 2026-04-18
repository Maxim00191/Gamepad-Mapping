using System.Collections.Generic;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Core;
using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services;

public sealed class CommunityTemplateUploadComplianceServiceTests
{
    [Fact]
    public void EvaluateSubmission_MoreThanMaxFiles_AddsSelectionError()
    {
        var sut = new CommunityTemplateUploadComplianceService();
        var selected = new List<CommunityTemplateBundleEntry>();
        for (var i = 0; i < CommunityTemplateUploadConstraints.MaxFilesPerSubmission + 1; i++)
        {
            selected.Add(new CommunityTemplateBundleEntry(
                $"game/author/profile-{i}",
                new GameProfileTemplate { ProfileId = $"profile-{i}" }));
        }

        var result = sut.EvaluateSubmission(
            selected,
            "Game",
            "Author",
            "Description");

        Assert.False(result.ReadyToSubmit);
        var selectionStep = Assert.Single(result.Steps, step => step.TitleKey == "CommunityUpload_Step_Selection_Title");
        Assert.Equal(CommunityTemplateComplianceSeverity.Error, selectionStep.Severity);
        Assert.Contains(
            selectionStep.Issues,
            issue => issue.SuggestionKey == "CommunityUpload_Suggest_MaxFilesPerUpload");
    }

    [Fact]
    public void EvaluateSubmission_TemplateLargerThanLimit_AddsContentError()
    {
        var sut = new CommunityTemplateUploadComplianceService();
        var oversized = new string('X', CommunityTemplateUploadConstraints.MaxTemplateFileBytes + 2048);
        var selected = new List<CommunityTemplateBundleEntry>
        {
            new(
                "game/author/profile-1",
                new GameProfileTemplate
                {
                    ProfileId = "profile-1",
                    DisplayName = oversized
                })
        };

        var result = sut.EvaluateSubmission(
            selected,
            "Game",
            "Author",
            "Description");

        Assert.False(result.ReadyToSubmit);
        var contentStep = Assert.Single(result.Steps, step => step.TitleKey == "CommunityUpload_Step_Content_Title");
        Assert.Equal(CommunityTemplateComplianceSeverity.Error, contentStep.Severity);
        Assert.Contains(
            contentStep.Issues,
            issue => issue.SuggestionKey == "CommunityUpload_Suggest_TemplateFileSizeLimit");
    }

    [Fact]
    public void EvaluateSubmission_ListingMatchesPolicy_AddsTextPolicyStepError()
    {
        var pattern = new UploadTextPolicyPattern
        {
            Id = "test",
            Match = "DistinctivePolicyTokenX7",
            Mode = "contains"
        };
        var evaluator = new UploadTextPolicyEvaluator([pattern]);
        var sut = new CommunityTemplateUploadComplianceService(evaluator);
        var selected = new List<CommunityTemplateBundleEntry>
        {
            new("game/author/p1", new GameProfileTemplate { ProfileId = "p1" })
        };

        var result = sut.EvaluateSubmission(
            selected,
            "Game",
            "Author",
            "Hello DistinctivePolicyTokenX7 world");

        Assert.False(result.ReadyToSubmit);
        var textPolicyStep = Assert.Single(result.Steps, step => step.TitleKey == CommunityTemplateUploadComplianceStepKeys.TextPolicyTitle);
        Assert.Equal(CommunityTemplateComplianceSeverity.Error, textPolicyStep.Severity);
        // Listing text is also copied onto templates as CommunityListingDescription, so the same hit can
        // appear for more than one field.
        Assert.Contains(
            textPolicyStep.Issues,
            issue => issue.SuggestionKey == "CommunityUpload_Suggest_TextPolicyViolation"
                     && issue.DetailResourceKey == CommunityTemplateComplianceDetailKeys.TextPolicyFieldViolationWithFieldText
                     && issue.DetailFormatArguments is ["Listing description", "Hello DistinctivePolicyTokenX7 world"]);
    }

    [Fact]
    public void EvaluateSubmission_ListingContainsUrl_UsesLinkSuggestionKey()
    {
        var sut = new CommunityTemplateUploadComplianceService();
        var selected = new List<CommunityTemplateBundleEntry>
        {
            new("game/author/p1", new GameProfileTemplate { ProfileId = "p1" })
        };

        var result = sut.EvaluateSubmission(
            selected,
            "Game",
            "Author",
            "Read more at https://example.com please");

        Assert.False(result.ReadyToSubmit);
        var textPolicyStep = Assert.Single(result.Steps,
            step => step.TitleKey == CommunityTemplateUploadComplianceStepKeys.TextPolicyTitle);
        Assert.Contains(
            textPolicyStep.Issues,
            issue => issue.SuggestionKey == "CommunityUpload_Suggest_LinkOrDomainNotAllowed");
    }

    [Fact]
    public void EvaluateSubmission_AuthorTooLong_AddsSubmissionError()
    {
        var sut = new CommunityTemplateUploadComplianceService();
        var selected = new List<CommunityTemplateBundleEntry>
        {
            new("game/author/p1", new GameProfileTemplate { ProfileId = "p1" })
        };
        var longAuthor = new string('a', CommunityTemplateUploadConstraints.MaxAuthorDisplayNameLength + 1);

        var result = sut.EvaluateSubmission(selected, "Game", longAuthor, "Description");

        Assert.False(result.ReadyToSubmit);
        var step = Assert.Single(result.Steps, s => s.TitleKey == CommunityTemplateUploadComplianceStepKeys.SubmissionTitle);
        Assert.Equal(CommunityTemplateComplianceSeverity.Error, step.Severity);
        Assert.Contains(
            step.Issues,
            i => i.DetailResourceKey == CommunityTemplateComplianceDetailKeys.AuthorNameTooLong);
    }

    [Fact]
    public void EvaluateSubmission_ListingTooLong_AddsSubmissionError()
    {
        var sut = new CommunityTemplateUploadComplianceService();
        var selected = new List<CommunityTemplateBundleEntry>
        {
            new("game/author/p1", new GameProfileTemplate { ProfileId = "p1" })
        };
        var longListing = new string('x', CommunityTemplateUploadConstraints.MaxListingDescriptionCharacters + 1);

        var result = sut.EvaluateSubmission(selected, "Game", "Author", longListing);

        Assert.False(result.ReadyToSubmit);
        var step = Assert.Single(result.Steps, s => s.TitleKey == CommunityTemplateUploadComplianceStepKeys.SubmissionTitle);
        Assert.Equal(CommunityTemplateComplianceSeverity.Error, step.Severity);
        Assert.Contains(
            step.Issues,
            i => i.DetailResourceKey == CommunityTemplateComplianceDetailKeys.ListingDescriptionTooLong);
    }
}
