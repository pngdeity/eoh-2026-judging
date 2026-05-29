using System.Collections.Generic;

using ContestJudging.Web.Services;

using Xunit;

namespace ContestJudging.Web.Tests;

[Trait("Category", "Unit")]
public class SetupTests
{
    [Fact]
    public void ParseNewEntries_SplitsAndTrimsLines()
    {
        var result = EntryBulkImporter.ParseNewEntries(
            "  A  \nB\r\nC",
            new HashSet<string>());

        Assert.Equal(3, result.Count);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
        Assert.Contains("C", result);
    }

    [Fact]
    public void ParseNewEntries_DeduplicatesInput()
    {
        var result = EntryBulkImporter.ParseNewEntries(
            "A\nA\nA",
            new HashSet<string>());

        Assert.Single(result);
        Assert.Equal("A", result[0]);
    }

    [Fact]
    public void ParseNewEntries_ExcludesExistingEntries()
    {
        var result = EntryBulkImporter.ParseNewEntries(
            "A\nB\nC",
            new HashSet<string> { "A", "C" });

        Assert.Single(result);
        Assert.Equal("B", result[0]);
    }

    [Fact]
    public void ParseNewEntries_AllExisting_ReturnsEmpty()
    {
        var result = EntryBulkImporter.ParseNewEntries(
            "A\nB",
            new HashSet<string> { "A", "B" });

        Assert.Empty(result);
    }

    [Fact]
    public void ParseNewEntries_EmptyString_ReturnsEmpty()
    {
        var result = EntryBulkImporter.ParseNewEntries(
            "",
            new HashSet<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void ParseNewEntries_WhitespaceOnly_ReturnsEmpty()
    {
        var result = EntryBulkImporter.ParseNewEntries(
            "   \n  \n   ",
            new HashSet<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void ParseNewEntries_HandlesMixedWhitespace()
    {
        var result = EntryBulkImporter.ParseNewEntries(
            "  \n  A  \n  \nB  \n  ",
            new HashSet<string>());

        Assert.Equal(2, result.Count);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
    }
}
