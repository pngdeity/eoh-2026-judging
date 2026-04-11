using System;
using System.Collections.Generic;
using System.Linq;

namespace ContestJudging.Core.Entities
{
    public class Entry
    {
        public string Id { get; }
        private readonly Dictionary<string, double> _scores = new();

        public Entry(string id)
        {
            Id = id;
        }

        public void SetScore(Category category, double score)
        {
            if (score >= 0 && score <= category.MaxScore)
            {
                _scores[category.Id] = score;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(score), $"Score {score} out of bounds for category '{category.Id}'.");
            }
        }

        public double TotalScore => _scores.Values.Sum();

        public IReadOnlyDictionary<string, double> Scores => _scores;
    }
}
