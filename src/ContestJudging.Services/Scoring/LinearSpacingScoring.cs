using System;
using System.Collections.Generic;

using ContestJudging.Core.Interfaces;

namespace ContestJudging.Services.Scoring
{
    public class LinearSpacingScoring : IScoringStrategy
    {
        public Dictionary<string, double> CalculateScores(List<HashSet<string>> sortedTiers, double maxScore)
        {
            var assignedScores = new Dictionary<string, double>();
            int k = sortedTiers.Count;

            if (k == 0) return assignedScores;

            if (k == 1)
            {
                foreach (var entryId in sortedTiers[0])
                {
                    assignedScores[entryId] = maxScore;
                }
                return assignedScores;
            }

            for (int i = 0; i < k; i++)
            {
                double tierScore = ((double)i / (k - 1)) * maxScore;
                foreach (var entryId in sortedTiers[i])
                {
                    assignedScores[entryId] = Math.Round(tierScore, 2);
                }
            }

            return assignedScores;
        }
    }
}
