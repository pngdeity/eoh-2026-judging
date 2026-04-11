using System.Collections.Generic;
using System.Linq;

using ContestJudging.Core.Interfaces;

namespace ContestJudging.Services.Scoring
{
    public class DefinedIntervalScoring : IScoringStrategy
    {
        private readonly List<double> _rankPoints;

        public DefinedIntervalScoring(IEnumerable<double> rankPoints)
        {
            _rankPoints = rankPoints.ToList();
        }

        public Dictionary<string, double> CalculateScores(List<HashSet<string>> sortedTiers, double maxScore)
        {
            var assignedScores = new Dictionary<string, double>();
            int k = sortedTiers.Count;

            if (k == 0) return assignedScores;

            for (int i = 0; i < k; i++)
            {
                int rankIndex = k - 1 - i;
                double tierScore = rankIndex < _rankPoints.Count ? _rankPoints[rankIndex] : 0;

                foreach (var entryId in sortedTiers[i])
                {
                    assignedScores[entryId] = tierScore;
                }
            }

            return assignedScores;
        }

        public Dictionary<string, double> CalculateScoresFromStrengths(Dictionary<string, double> globalStrengths, double maxScore)
        {
            // Fallback to simple linear scaling if strengths are provided to a tiered scoring system
            var assignedScores = new Dictionary<string, double>();
            if (globalStrengths.Count == 0) return assignedScores;

            double minStrength = globalStrengths.Values.Min();
            double maxStrength = globalStrengths.Values.Max();
            double range = maxStrength - minStrength;

            foreach (var kvp in globalStrengths)
            {
                double normalized = range < 1e-9 ? 1.0 : (kvp.Value - minStrength) / range;
                assignedScores[kvp.Key] = Math.Round(normalized * maxScore, 2);
            }

            return assignedScores;
        }
    }
}
