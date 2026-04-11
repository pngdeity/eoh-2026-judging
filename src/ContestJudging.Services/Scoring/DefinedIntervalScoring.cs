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

            // sortedTiers is [lowest, ..., highest]
            // We want to map highest (k-1) to rankPoints[0], ..., lowest (0) to rankPoints[k-1]
            for (int i = 0; i < k; i++)
            {
                int rankIndex = k - 1 - i;
                double tierScore = rankIndex < rankPoints.Count ? rankPoints[rankIndex] : 0;

                foreach (var entryId in sortedTiers[i])
                {
                    assignedScores[entryId] = tierScore;
                }
            }
            }

            return assignedScores;
        }
    }
}
