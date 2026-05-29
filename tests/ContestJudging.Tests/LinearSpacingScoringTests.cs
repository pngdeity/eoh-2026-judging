using System.Collections.Generic;
using System.Linq;

using ContestJudging.Services.Scoring;

using Xunit;

namespace ContestJudging.Tests
{
    [Trait("Category", "Unit")]
    public class LinearSpacingScoringTests
    {
        [Fact]
        public void LinearSpacingScoring_CalculatesCorrectly()
        {
            var strategy = new LinearSpacingScoring();
            var tiers = new List<HashSet<string>>
            {
                new HashSet<string> { "A" },
                new HashSet<string> { "B", "C" },
                new HashSet<string> { "D" }
            };

            var scores = strategy.CalculateScores(tiers, 100);

            Assert.Equal(0, scores["A"]);
            Assert.Equal(50, scores["B"]);
            Assert.Equal(50, scores["C"]);
            Assert.Equal(100, scores["D"]);
        }

        [Fact]
        public void LinearSpacing_CalculateScoresFromStrengths_VariedStrengths_ReturnsScaledScores()
        {
            var strategy = new LinearSpacingScoring();
            var strengths = new Dictionary<string, double> { { "A", 3.0 }, { "B", 1.0 }, { "C", 0.0 } };
            var scores = strategy.CalculateScoresFromStrengths(strengths, 100);

            Assert.Equal(3, scores.Count);
            Assert.True(scores["A"] > 90.0, "Highest strength should get near max");
            Assert.True(scores["C"] < 10.0, "Lowest strength should get near min");
        }

        [Fact]
        public void LinearSpacing_CalculateScoresFromStrengths_AllSameStrength_AllGetMaxScore()
        {
            var strategy = new LinearSpacingScoring();
            var strengths = new Dictionary<string, double> { { "A", 5.0 }, { "B", 5.0 } };
            var scores = strategy.CalculateScoresFromStrengths(strengths, 100);

            Assert.Equal(100, scores["A"]);
            Assert.Equal(100, scores["B"]);
        }

        [Fact]
        public void LinearSpacing_CalculateScoresFromStrengths_SingleEntry_GetsMaxScore()
        {
            var strategy = new LinearSpacingScoring();
            var strengths = new Dictionary<string, double> { { "A", 0.5 } };
            var scores = strategy.CalculateScoresFromStrengths(strengths, 10);

            Assert.Single(scores);
            Assert.Equal(10, scores["A"]);
        }

        [Fact]
        public void LinearSpacingScoring_CalculateScores_EmptyTiers_ReturnsEmpty()
        {
            var strategy = new LinearSpacingScoring();
            var result = strategy.CalculateScores(new List<HashSet<string>>(), 100);
            Assert.Empty(result);
        }
    }
}
