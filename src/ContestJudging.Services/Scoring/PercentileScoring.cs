using System;
using System.Collections.Generic;
using System.Linq;

using ContestJudging.Core.Interfaces;

namespace ContestJudging.Services.Scoring
{
    public class PercentileScoring : IScoringStrategy
    {
        public Dictionary<string, double> CalculateScores(List<HashSet<string>> sortedTiers, double maxScore)
        {
            var assignedScores = new Dictionary<string, double>();
            int totalEntries = sortedTiers.Sum(t => t.Count);

            if (totalEntries == 0) return assignedScores;
            if (totalEntries == 1 || sortedTiers.Count == 1)
            {
                foreach (var entryId in sortedTiers[0])
                {
                    assignedScores[entryId] = maxScore;
                }
                return assignedScores;
            }

            int beatenOpponents = 0;
            foreach (var tier in sortedTiers)
            {
                double score = ((double)beatenOpponents / (totalEntries - 1)) * maxScore;
                foreach (var entryId in tier)
                {
                    assignedScores[entryId] = Math.Round(score, 2);
                }
                beatenOpponents += tier.Count;
            }

            return assignedScores;
        }

        public Dictionary<string, double> CalculateScoresFromStrengths(Dictionary<string, double> globalStrengths, double maxScore)
        {
            // Percentile for continuous strengths: rank entries by strength and use their percentile
            var assignedScores = new Dictionary<string, double>();
            if (globalStrengths.Count == 0) return assignedScores;
            if (globalStrengths.Count == 1)
            {
                assignedScores[globalStrengths.Keys.First()] = maxScore;
                return assignedScores;
            }

            var sortedEntries = globalStrengths.OrderBy(kvp => kvp.Value).ToList();
            int n = sortedEntries.Count;

            for (int i = 0; i < n; i++)
            {
                double score = ((double)i / (n - 1)) * maxScore;
                assignedScores[sortedEntries[i].Key] = Math.Round(score, 2);
            }

            return assignedScores;
        }
    }
}
