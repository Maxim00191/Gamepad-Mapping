using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services;

public sealed class UploadTextPolicyTextParserTests
{
    [Fact]
    public void Parse_TabSeparated_PreservesMatchWithSpaces()
    {
        var text = "a\tcontains\thello world\n";

        var rows = UploadTextPolicyTextParser.Parse(text);

        var row = Assert.Single(rows);
        Assert.Equal("a", row.Id);
        Assert.Equal("contains", row.Mode);
        Assert.Equal("hello world", row.Match);
    }

    [Fact]
    public void Parse_SkipsCommentsAndEmpty()
    {
        var text = "# comment\n\n  \nid\twholeWord\tword\n";

        var rows = UploadTextPolicyTextParser.Parse(text);

        var row = Assert.Single(rows);
        Assert.Equal("id", row.Id);
        Assert.Equal("wholeWord", row.Mode);
        Assert.Equal("word", row.Match);
    }
}
