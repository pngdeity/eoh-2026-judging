using System.Collections.Generic;
using System.Linq;

using ContestJudging.Services.Scoring;

using Xunit;

namespace ContestJudging.Tests
{
    [Trait("Category", "Unit")]
    [Trait("Category", "Unit")]
    public class ScoringStrategyTests
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
        public void PercentileScoring_CalculatesCorrectly()
        {
            var strategy = new PercentileScoring();
            var tiers = new List<HashSet<string>>
            {
                new HashSet<string> { "A", "B" }, // Beaten 0/4
                new HashSet<string> { "C" },      // Beaten 2/4
                new HashSet<string> { "D", "E" }  // Beaten 3/4
            };

            var scores = strategy.CalculateScores(tiers, 10);

            Assert.Equal(0, scores["A"]);
            Assert.Equal(0, scores["B"]);
            Assert.Equal(5.0, scores["C"]);
            Assert.Equal(7.5, scores["D"]);
            Assert.Equal(7.5, scores["E"]);
        }

        [Fact]
        public void DefinedIntervalScoring_CalculatesCorrectly()
        {
            var rankPoints = new List<double> { 10, 8, 5 };
            var strategy = new DefinedIntervalScoring(rankPoints);
            var tiers = new List<HashSet<string>>
            {
                new HashSet<string> { "A" },      // Lowest: rank 2 (index 2) -> 5
                new HashSet<string> { "B", "C" }, // Mid: rank 1 (index 1) -> 8
                new HashSet<string> { "D" }       // Highest: rank 0 (index 0) -> 10
            };

            var scores = strategy.CalculateScores(tiers, 10);

            Assert.Equal(5, scores["A"]);
            Assert.Equal(8, scores["B"]);
            Assert.Equal(8, scores["C"]);
            Assert.Equal(10, scores["D"]);
        }

        [Fact]
        public void DefinedIntervalScoring_FewerRanksThanTiers_AssignsZero()
        {
            var rankPoints = new List<double> { 10 };
            var strategy = new DefinedIntervalScoring(rankPoints);
            var tiers = new List<HashSet<string>>
            {
                new HashSet<string> { "A" },
                new HashSet<string> { "B" }
            };

            var scores = strategy.CalculateScores(tiers, 10);

            Assert.Equal(10, scores["B"]); // Highest
            Assert.Equal(0, scores["A"]);  // Lowest, no rank points left
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
        public void Percentile_CalculateScoresFromStrengths_RanksByStrengthPercentile()
        {
            var strategy = new PercentileScoring();
            var strengths = new Dictionary<string, double> { { "A", 3.0 }, { "B", 2.0 }, { "C", 1.0 } };
            var scores = strategy.CalculateScoresFromStrengths(strengths, 10);

            Assert.Equal(3, scores.Count);
            Assert.True(scores["A"] > scores["B"]);
            Assert.True(scores["B"] > scores["C"]);
        }

        [Fact]
        public void DefinedInterval_CalculateScoresFromStrengths_LinearScalingFallback()
        {
            var strategy = new DefinedIntervalScoring(new List<double> { 10, 8, 5 });
            var strengths = new Dictionary<string, double> { { "A", 3.0 }, { "B", 2.0 }, { "C", 1.0 } };
            var scores = strategy.CalculateScoresFromStrengths(strengths, 100);

            Assert.Equal(3, scores.Count);
            Assert.True(scores["A"] > scores["B"]);
            Assert.True(scores["B"] > scores["C"]);
        }

        [Fact]
        public void LinearSpacingScoring_CalculateScores_EmptyTiers_ReturnsEmpty()
        {
            var strategy = new LinearSpacingScoring();
            var result = strategy.CalculateScores(new List<HashSet<string>>(), 100);
            Assert.Empty(result);
        }

        [Fact]
        public void PercentileScoring_CalculateScores_EmptyTiers_ReturnsEmpty()
        {
            var strategy = new PercentileScoring();
            var result = strategy.CalculateScores(new List<HashSet<string>>(), 10);
            Assert.Empty(result);
        }

        [Fact]
        public void DefinedIntervalScoring_CalculateScores_EmptyTiers_ReturnsEmpty()
        {
            var strategy = new DefinedIntervalScoring(new List<double> { 10, 8, 5 });
            var result = strategy.CalculateScores(new List<HashSet<string>>(), 10);
            Assert.Empty(result);
        }
    }
}
