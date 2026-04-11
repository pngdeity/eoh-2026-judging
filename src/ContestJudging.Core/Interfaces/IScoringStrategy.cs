using System.Collections.Generic;

namespace ContestJudging.Core.Interfaces
{
    public interface IScoringStrategy
    {
        Dictionary<string, double> CalculateScores(List<HashSet<string>> sortedTiers, double maxScore);
    }
}
