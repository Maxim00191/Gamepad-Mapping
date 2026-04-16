using GamepadMapperGUI.Models;
using Gamepad_Mapping.Utils.Community;
using Xunit;

namespace GamepadMapping.Tests.Utils.Community;

public class CommunityTemplateIndexSearchTests
{
    [Fact]
    public void Filter_EmptyQuery_ReturnsAll()
    {
        var list = new List<CommunityTemplateInfo>
        {
            new() { Id = "a", DisplayName = "One" },
            new() { Id = "b", DisplayName = "Two" }
        };

        var filtered = CommunityTemplateIndexSearch.Filter(list, "");
        var filteredWs = CommunityTemplateIndexSearch.Filter(list, "   ");

        Assert.Equal(2, filtered.Count);
        Assert.Equal(2, filteredWs.Count);
    }

    [Fact]
    public void Filter_MatchesDisplayName_CaseInsensitive()
    {
        var list = new List<CommunityTemplateInfo>
        {
            new() { Id = "x", DisplayName = "Elden Ring" }
        };

        var hit = CommunityTemplateIndexSearch.Filter(list, "elden");
        var miss = CommunityTemplateIndexSearch.Filter(list, "zelda");

        Assert.Single(hit);
        Assert.Empty(miss);
    }

    [Fact]
    public void Filter_MultipleTerms_AndSemantics()
    {
        var list = new List<CommunityTemplateInfo>
        {
            new() { Id = "1", DisplayName = "Alpha Beta", Author = "Gamma" },
            new() { Id = "2", DisplayName = "Alpha Only", Author = "Delta" }
        };

        var both = CommunityTemplateIndexSearch.Filter(list, "alpha beta");
        var one = CommunityTemplateIndexSearch.Filter(list, "alpha gamma");

        Assert.Single(both);
        Assert.Equal("1", both[0].Id);
        Assert.Single(one);
        Assert.Equal("1", one[0].Id);
    }

    [Fact]
    public void Filter_SearchesTags_IgnoresDownloadUrl()
    {
        var list = new List<CommunityTemplateInfo>
        {
            new()
            {
                Id = "id1",
                DisplayName = "N",
                Tags = ["steam"],
                DownloadUrl = "https://example.com/raw/repo/main/game/t.json"
            }
        };

        var byTag = CommunityTemplateIndexSearch.Filter(list, "steam");
        var byUrl = CommunityTemplateIndexSearch.Filter(list, "example.com");

        Assert.Single(byTag);
        Assert.Empty(byUrl);
    }

    [Fact]
    public void Filter_ExtraSpacesBetweenTerms_StillMatches()
    {
        var list = new List<CommunityTemplateInfo>
        {
            new() { Id = "1", DisplayName = "Foo Bar" }
        };

        var hit = CommunityTemplateIndexSearch.Filter(list, "foo    bar");
        Assert.Single(hit);
    }
}
