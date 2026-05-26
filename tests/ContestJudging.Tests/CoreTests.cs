using System;

using ContestJudging.Core.Entities;

using Xunit;

namespace ContestJudging.Tests
{
    [Trait("Category", "Unit")]
    [Trait("Category", "Unit")]
    public class EntityValidationTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(-1)]
        public void Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess(double maxScore)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Category("cat1", maxScore));
        }

        [Fact]
        public void Category_Constructor_SetsProperties()
        {
            var category = new Category("cat1", 10);
            Assert.Equal("cat1", category.Id);
            Assert.Equal(10, category.MaxScore);
        }

        [Fact]
        public void Entry_SetScore_ValidScore_Succeeds()
        {
            var entry = new Entry("entry1");
            var category = new Category("cat1", 10);
            entry.SetScore(category, 5);
            Assert.Equal(5, entry.Scores["cat1"]);
        }

        [Theory]
        [InlineData(11)]
        [InlineData(-1)]
        public void Entry_SetScore_InvalidScore_Throws(double score)
        {
            var entry = new Entry("entry1");
            var category = new Category("cat1", 10);
            Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetScore(category, score));
        }

        [Fact]
        public void Entry_TotalScore_SumsAllCategoryScores()
        {
            var entry = new Entry("entry1");
            var cat1 = new Category("cat1", 10);
            var cat2 = new Category("cat2", 20);
            entry.SetScore(cat1, 5);
            entry.SetScore(cat2, 15);
            Assert.Equal(20, entry.TotalScore);
        }

        [Fact]
        public void Entry_SetScore_ZeroScore_Succeeds()
        {
            var entry = new Entry("entry1");
            var category = new Category("cat1", 10);
            entry.SetScore(category, 0);
            Assert.Equal(0, entry.Scores["cat1"]);
        }

        [Fact]
        public void Entry_SetScore_MaxScore_Succeeds()
        {
            var entry = new Entry("entry1");
            var category = new Category("cat1", 10);
            entry.SetScore(category, 10);
            Assert.Equal(10, entry.Scores["cat1"]);
        }

        [Fact]
        public void Entry_SetScore_AboveMaxScore_Throws()
        {
            var entry = new Entry("entry1");
            var category = new Category("cat1", 10);
            Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetScore(category, 11));
        }
    }
}
