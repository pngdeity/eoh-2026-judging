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
    }
}
