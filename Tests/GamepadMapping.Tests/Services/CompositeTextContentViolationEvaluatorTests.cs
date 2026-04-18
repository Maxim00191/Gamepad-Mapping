using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services;

public sealed class CompositeTextContentViolationEvaluatorTests
{
    [Fact]
    public void Evaluate_LinkEvaluatorRunsFirst_WordlistDoesNotOverride()
    {
        var wordlist = new UploadTextPolicyEvaluator(
        [
            new UploadTextPolicyPattern { Id = "w", Match = "blocked", Mode = "contains" }
        ]);
        var sut = new CompositeTextContentViolationEvaluator(
            new UploadLinkPatternViolationEvaluator(),
            wordlist);

        var fields = new[]
        {
            new TextContentInspectionField("", "Listing description", "blocked https://x.example.com")
        };

        var r = sut.Evaluate(fields);

        var hit = Assert.Single(r);
        Assert.Equal(UploadLinkPatternViolationEvaluator.SuggestionResourceKey, hit.SuggestionResourceKey);
    }

    [Fact]
    public void Evaluate_WordlistUsedWhenNoLink()
    {
        var wordlist = new UploadTextPolicyEvaluator(
        [
            new UploadTextPolicyPattern { Id = "w", Match = "blocked", Mode = "contains" }
        ]);
        var sut = new CompositeTextContentViolationEvaluator(
            new UploadLinkPatternViolationEvaluator(),
            wordlist);

        var fields = new[]
        {
            new TextContentInspectionField("", "Listing description", "prefix blocked suffix")
        };

        var r = sut.Evaluate(fields);

        var hit = Assert.Single(r);
        Assert.Equal("w", hit.PatternId);
        Assert.Null(hit.SuggestionResourceKey);
        Assert.Equal("blocked", hit.MatchedSegmentHint);
        Assert.Equal("prefix blocked suffix", hit.ViolatingFieldText);
    }
}
