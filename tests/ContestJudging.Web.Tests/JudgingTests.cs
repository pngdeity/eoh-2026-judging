using System.Collections.Generic;

using ContestJudging.Core.Entities;
using ContestJudging.Web.Services;

using Xunit;

namespace ContestJudging.Web.Tests;

[Trait("Category", "Unit")]
public class JudgingTests
{
    [Fact]
    public void FindSuggestedPair_NoExistingPairs_ReturnsFirstPair()
    {
        var result = JudgingUtilities.FindSuggestedPair(
            new List<string> { "A", "B", "C" },
            new HashSet<(string, string)>());

        Assert.NotNull(result);
        Assert.Equal("A", result.Value.A);
        Assert.Equal("B", result.Value.B);
    }

    [Fact]
    public void FindSuggestedPair_SkipsExistingPairs()
    {
        var existing = new HashSet<(string, string)> { ("A", "B"), ("B", "A") };

        var result = JudgingUtilities.FindSuggestedPair(
            new List<string> { "A", "B", "C" },
            existing);

        Assert.NotNull(result);
        Assert.Equal("A", result.Value.A);
        Assert.Equal("C", result.Value.B);
    }

    [Fact]
    public void FindSuggestedPair_AllPairsExist_ReturnsNull()
    {
        var existing = new HashSet<(string, string)>
        {
            ("A", "B"), ("B", "A"),
            ("A", "C"), ("C", "A"),
            ("B", "C"), ("C", "B")
        };

        var result = JudgingUtilities.FindSuggestedPair(
            new List<string> { "A", "B", "C" },
            existing);

        Assert.Null(result);
    }

    [Fact]
    public void FindSuggestedPair_SingleEntry_ReturnsNull()
    {
        var result = JudgingUtilities.FindSuggestedPair(
            new List<string> { "A" },
            new HashSet<(string, string)>());

        Assert.Null(result);
    }

    [Fact]
    public void FindSuggestedPair_EmptyList_ReturnsNull()
    {
        var result = JudgingUtilities.FindSuggestedPair(
            new List<string>(),
            new HashSet<(string, string)>());

        Assert.Null(result);
    }

    [Theory]
    [InlineData("a", Operator.GreaterThan)]
    [InlineData("A", Operator.GreaterThan)]
    [InlineData("arrowleft", Operator.GreaterThan)]
    [InlineData("ArrowLeft", Operator.GreaterThan)]
    [InlineData("s", Operator.EqualTo)]
    [InlineData("arrowdown", Operator.EqualTo)]
    [InlineData("d", Operator.LessThan)]
    [InlineData("arrowright", Operator.LessThan)]
    public void MapKeyToOperator_ReturnsExpectedOperator(string key, Operator expected)
    {
        var result = JudgingUtilities.MapKeyToOperator(key);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapKeyToOperator_UnknownKey_ReturnsNull()
    {
        var result = JudgingUtilities.MapKeyToOperator("z");
        Assert.Null(result);
    }

    [Fact]
    public void ValidateRelationEntries_BothEmpty_ReturnsError()
    {
        var error = JudgingUtilities.ValidateRelationEntries("", "");
        Assert.NotNull(error);
        Assert.Contains("select both entries", error);
    }

    [Fact]
    public void ValidateRelationEntries_EntryAEmpty_ReturnsError()
    {
        var error = JudgingUtilities.ValidateRelationEntries("", "B");
        Assert.NotNull(error);
        Assert.Contains("select both entries", error);
    }

    [Fact]
    public void ValidateRelationEntries_SameId_ReturnsError()
    {
        var error = JudgingUtilities.ValidateRelationEntries("A", "A");
        Assert.NotNull(error);
        Assert.Contains("must be different", error);
    }

    [Fact]
    public void ValidateRelationEntries_ValidEntries_ReturnsNull()
    {
        var error = JudgingUtilities.ValidateRelationEntries("A", "B");
        Assert.Null(error);
    }

    [Theory]
    [InlineData(Operator.GreaterThan, "is better than")]
    [InlineData(Operator.LessThan, "is worse than")]
    [InlineData(Operator.EqualTo, "is equal to")]
    public void GetOperatorText_ReturnsExpectedDisplay(Operator op, string expectedContains)
    {
        var text = JudgingUtilities.GetOperatorText(op);
        Assert.Contains(expectedContains, text);
    }

    [Fact]
    public void GetOperatorText_UnknownOperator_ReturnsFallback()
    {
        var text = JudgingUtilities.GetOperatorText((Operator)999);
        Assert.Equal("unknown", text);
    }
}
