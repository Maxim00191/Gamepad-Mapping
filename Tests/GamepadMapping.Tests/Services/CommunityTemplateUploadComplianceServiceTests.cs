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
            Match = "___policy_test_marker___",
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
            "Hello ___policy_test_marker___ world");

        Assert.False(result.ReadyToSubmit);
        var textPolicyStep = Assert.Single(result.Steps, step => step.TitleKey == CommunityTemplateUploadComplianceStepKeys.TextPolicyTitle);
        Assert.Equal(CommunityTemplateComplianceSeverity.Error, textPolicyStep.Severity);
        Assert.Contains(
            textPolicyStep.Issues,
            issue => issue.SuggestionKey == "CommunityUpload_Suggest_TextPolicyViolation");
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
}
