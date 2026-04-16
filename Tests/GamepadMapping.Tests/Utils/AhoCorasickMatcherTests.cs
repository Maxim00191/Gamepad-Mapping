using System.Collections.Generic;
using GamepadMapperGUI.Utils.Text;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class AhoCorasickMatcherTests
{
    [Fact]
    public void Search_ClassicExample_FindsOverlappingPatterns()
    {
        var patterns = new[] { "he", "she", "his", "hers" };
        var sut = new AhoCorasickMatcher(patterns);
        var hits = new List<(int Idx, int End)>();
        sut.Search("ushers", (idx, end) => hits.Add((idx, end)));

        Assert.Contains((1, 4), hits);
        Assert.Contains((0, 4), hits);
        Assert.Contains((3, 6), hits);
    }

    [Fact]
    public void Search_SamePrefix_DistinctIndices()
    {
        var patterns = new[] { "bad", "badword" };
        var sut = new AhoCorasickMatcher(patterns);
        var found = new bool[2];
        sut.Search("xxbadwordyy", (idx, _) => found[idx] = true);
        Assert.True(found[0]);
        Assert.True(found[1]);
    }
}
