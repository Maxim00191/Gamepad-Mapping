using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services;

public sealed class UploadTextPolicyEvaluatorTests
{
    [Fact]
    public void Evaluate_ContainsMode_FindsSubstring()
    {
        var p = new UploadTextPolicyPattern
        {
            Id = "x",
            Match = "badphrase",
            Mode = "contains"
        };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[]
        {
            new TextContentInspectionField("", "Listing description", "Prefix badphrase suffix")
        };

        var r = sut.Evaluate(fields);

        var hit = Assert.Single(r);
        Assert.Equal("x", hit.PatternId);
    }

    [Fact]
    public void Evaluate_WholeWordMode_DoesNotMatchInsideWord()
    {
        var p = new UploadTextPolicyPattern { Id = "w", Match = "bad", Mode = "wholeWord" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "somebadword") };

        Assert.Empty(sut.Evaluate(fields));
    }

    [Fact]
    public void Evaluate_WholeWordMode_MatchesStandaloneToken()
    {
        var p = new UploadTextPolicyPattern { Id = "w", Match = "bad", Mode = "wholeWord" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "this bad here") };

        Assert.Single(sut.Evaluate(fields));
    }

    [Fact]
    public void Evaluate_FieldWithFullwidthComma_MatchesAsciiCommaPattern()
    {
        var p = new UploadTextPolicyPattern { Id = "p", Match = "x,y", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "prefix x，y suffix") };

        Assert.Single(sut.Evaluate(fields));
    }

    [Fact]
    public void Evaluate_FullwidthCommaInPattern_MatchesAsciiField()
    {
        var p = new UploadTextPolicyPattern { Id = "p", Match = "x，y", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "prefix x,y suffix") };

        Assert.Single(sut.Evaluate(fields));
    }

    [Fact]
    public void Evaluate_ContainsMode_FindsNeedleWhenSegmentedWithDots()
    {
        var p = new UploadTextPolicyPattern { Id = "seg", Match = "badphrase", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[]
        {
            new TextContentInspectionField("", "Field", "prefix b.a.d.p.h.r.a.s.e suffix")
        };

        var r = sut.Evaluate(fields);
        var hit = Assert.Single(r);
        Assert.Equal("seg", hit.PatternId);
    }

    [Fact]
    public void Evaluate_WholeWordMode_StillRespectsSpaces()
    {
        var p = new UploadTextPolicyPattern { Id = "w", Match = "bad", Mode = "wholeWord" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "this bad here") };

        Assert.Single(sut.Evaluate(fields));
    }
}
