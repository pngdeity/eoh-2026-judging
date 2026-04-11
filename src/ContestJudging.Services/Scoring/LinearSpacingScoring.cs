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

        public Dictionary<string, double> CalculateScoresFromStrengths(Dictionary<string, double> globalStrengths, double maxScore)
        {
            var assignedScores = new Dictionary<string, double>();
            if (globalStrengths.Count == 0) return assignedScores;

            double minStrength = double.MaxValue;
            double maxStrength = double.MinValue;

            foreach (var strength in globalStrengths.Values)
            {
                if (strength < minStrength) minStrength = strength;
                if (strength > maxStrength) maxStrength = strength;
            }

            double range = maxStrength - minStrength;

            foreach (var kvp in globalStrengths)
            {
                double normalized;
                if (range < 1e-9) // All same strength or single entry
                {
                    normalized = 1.0;
                }
                else
                {
                    normalized = (kvp.Value - minStrength) / range;
                }

                assignedScores[kvp.Key] = Math.Round(normalized * maxScore, 2);
            }

            return assignedScores;
        }
    }
}
