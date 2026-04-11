using System;

using ContestJudging.Core.Entities;

using Xunit;

namespace ContestJudging.Tests
{
    public class CoreTests
    {
        [Fact]
        public void Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Category("cat1", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Category("cat1", 0));
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

        [Fact]
        public void Entry_SetScore_InvalidScore_Throws()
        {
            var entry = new Entry("entry1");
            var category = new Category("cat1", 10);
            Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetScore(category, 11));
            Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetScore(category, -1));
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
    }
}
