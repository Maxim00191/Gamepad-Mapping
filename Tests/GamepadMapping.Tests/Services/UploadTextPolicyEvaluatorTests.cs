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
        Assert.Equal("badphrase", hit.MatchedSegmentHint);
        Assert.Equal("Prefix badphrase suffix", hit.ViolatingFieldText);
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
    public void Evaluate_ContainsMode_MatchesWhenLettersAreSplitByObfuscationPunctuation()
    {
        var p = new UploadTextPolicyPattern { Id = "seg", Match = "badphrase", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[]
        {
            new TextContentInspectionField("", "Field", "prefix b.a.d.p.h.r.a.s.e suffix")
        };

        var hit = Assert.Single(sut.Evaluate(fields));
        Assert.Equal("seg", hit.PatternId);
        Assert.Equal("badphrase", hit.MatchedSegmentHint);
    }

    [Fact]
    public void Evaluate_ContainsMode_DiscardsNeedleWithAsteriskBetweenLetters()
    {
        var p = new UploadTextPolicyPattern { Id = "mask", Match = "f*ck", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "prefix f*ck suffix") };

        Assert.Empty(sut.Evaluate(fields));
    }

    [Fact]
    public void Evaluate_ContainsMode_CleanNeedleMatchesInterLetterSlashObfuscation()
    {
        var p = new UploadTextPolicyPattern { Id = "w", Match = "badphrase", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[]
        {
            new TextContentInspectionField("", "Field", "prefix b/a/d/p/h/r/a/s/e suffix")
        };

        var hit = Assert.Single(sut.Evaluate(fields));
        Assert.Equal("badphrase", hit.MatchedSegmentHint);
    }

    [Fact]
    public void Evaluate_WholeWordMode_StillRespectsSpaces()
    {
        var p = new UploadTextPolicyPattern { Id = "w", Match = "bad", Mode = "wholeWord" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "this bad here") };

        Assert.Single(sut.Evaluate(fields));
    }

    [Fact]
    public void Evaluate_ContainsMode_PlusPrefixPhrase_IsNotReducedToSingleLetter()
    {
        var p = new UploadTextPolicyPattern { Id = "contact", Match = "+q", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "suffix q prefix") };

        Assert.Empty(sut.Evaluate(fields));
    }

    [Fact]
    public void Evaluate_ContainsMode_PlusPrefixPhrase_MatchesLiteral()
    {
        var p = new UploadTextPolicyPattern { Id = "contact", Match = "+q", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "+q reach me today") };

        var hit = Assert.Single(sut.Evaluate(fields));
        Assert.Equal("contact", hit.PatternId);
        Assert.Equal("+q", hit.MatchedSegmentHint);
    }

    [Fact]
    public void Evaluate_ContainsMode_FullwidthColonSuffix_IsNotReducedToSingleLetter()
    {
        var p = new UploadTextPolicyPattern { Id = "suffix", Match = "q：", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "only q here") };

        Assert.Empty(sut.Evaluate(fields));
    }

    [Fact]
    public void Evaluate_ContainsMode_FullwidthColonSuffix_MatchesLiteral()
    {
        var p = new UploadTextPolicyPattern { Id = "suffix", Match = "q：", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var fields = new[] { new TextContentInspectionField("", "Field", "label q：") };

        var hit = Assert.Single(sut.Evaluate(fields));
        Assert.Equal("suffix", hit.PatternId);
        Assert.Equal("q：", hit.MatchedSegmentHint);
        Assert.Equal("label q：", hit.ViolatingFieldText);
    }

    [Fact]
    public void Evaluate_ViolatingFieldText_TruncatesWhenAboveDisplayLimit()
    {
        var p = new UploadTextPolicyPattern { Id = "x", Match = "BAD", Mode = "contains" };
        var sut = new UploadTextPolicyEvaluator([p]);
        var pad = new string('a', CommunityTemplateUploadConstraints.MaxComplianceIssueFieldDisplayCharacters);
        var fields = new[] { new TextContentInspectionField("", "Field", pad + "BAD") };

        var hit = Assert.Single(sut.Evaluate(fields));
        Assert.Equal(
            CommunityTemplateUploadConstraints.MaxComplianceIssueFieldDisplayCharacters + 1,
            hit.ViolatingFieldText!.Length);
    }
}
