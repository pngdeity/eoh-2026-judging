using System.Collections.Generic;

namespace ContestJudging.Core.Interfaces
{
    public interface IScoringStrategy
    {
        // Original method for strict tier systems
        Dictionary<string, double> CalculateScores(List<HashSet<string>> sortedTiers, double maxScore);

        // NEW: Method for statistical models
        Dictionary<string, double> CalculateScoresFromStrengths(Dictionary<string, double> globalStrengths, double maxScore);
    }
}
