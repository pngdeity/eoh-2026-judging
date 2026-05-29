using System.Collections.Generic;

using ContestJudging.Core.Entities;
using ContestJudging.Web.Services;

using Xunit;

namespace ContestJudging.Web.Tests;

[Trait("Category", "Unit")]
public class ResultsTests
{
    [Fact]
    public void Build_SortsByTotalScoreDescending()
    {
        var entryA = new Entry("A");
        var entryB = new Entry("B");
        var entryC = new Entry("C");

        var cat = new Category("cat1", 100);
        entryA.SetScore(cat, 30);
        entryB.SetScore(cat, 90);
        entryC.SetScore(cat, 60);

        var result = LeaderboardBuilder.Build(new List<Entry> { entryA, entryB, entryC });

        Assert.Equal(3, result.Count);
        Assert.Equal("B", result[0].Entry.Id);
        Assert.Equal("C", result[1].Entry.Id);
        Assert.Equal("A", result[2].Entry.Id);
    }

    [Fact]
    public void Build_AssignsRanksCorrectly()
    {
        var cat = new Category("cat1", 100);
        var entryA = new Entry("A");
        var entryB = new Entry("B");
        entryA.SetScore(cat, 50);
        entryB.SetScore(cat, 80);

        var result = LeaderboardBuilder.Build(new List<Entry> { entryA, entryB });

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Rank);
        Assert.Equal(2, result[1].Rank);
        Assert.Equal("B", result[0].Entry.Id);
    }

    [Fact]
    public void Build_EmptyList_ReturnsEmpty()
    {
        var result = LeaderboardBuilder.Build(new List<Entry>());
        Assert.Empty(result);
    }

    [Fact]
    public void Build_SingleEntry_AssignsRankOne()
    {
        var entry = new Entry("A");

        var result = LeaderboardBuilder.Build(new List<Entry> { entry });

        Assert.Single(result);
        Assert.Equal(1, result[0].Rank);
        Assert.Equal("A", result[0].Entry.Id);
    }

    [Fact]
    public void Build_SameScoreEntries_MaintainsOrder()
    {
        var cat = new Category("cat1", 100);
        var entryA = new Entry("A");
        var entryB = new Entry("B");
        entryA.SetScore(cat, 50);
        entryB.SetScore(cat, 50);

        var result = LeaderboardBuilder.Build(new List<Entry> { entryA, entryB });

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Rank);
        Assert.Equal(2, result[1].Rank);
    }

    [Fact]
    public void Build_EntriesWithNoScores_ZeroTotalScore()
    {
        var entryA = new Entry("A");
        var entryB = new Entry("B");

        var result = LeaderboardBuilder.Build(new List<Entry> { entryA, entryB });

        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].Entry.TotalScore);
        Assert.Equal(0, result[1].Entry.TotalScore);
    }
}
