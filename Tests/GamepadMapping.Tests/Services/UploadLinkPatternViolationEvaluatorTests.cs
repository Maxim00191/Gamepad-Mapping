using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services;

public sealed class UploadLinkPatternViolationEvaluatorTests
{
    [Fact]
    public void Evaluate_FindsUrlInField()
    {
        var sut = new UploadLinkPatternViolationEvaluator();
        var fields = new[]
        {
            new TextContentInspectionField("", "Game folder name", "ok"),
            new TextContentInspectionField("", "Listing description", "see https://a.example.com/x")
        };

        var r = sut.Evaluate(fields);

        var hit = Assert.Single(r);
        Assert.Equal(UploadLinkPatternViolationEvaluator.SuggestionResourceKey, hit.SuggestionResourceKey);
    }
}
