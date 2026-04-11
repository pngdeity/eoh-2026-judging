using System;

namespace ContestJudging.Core.Entities
{
    public class Category
    {
        public string Id { get; }
        public double MaxScore { get; }

        public Category(string id, double maxScore)
        {
            if (maxScore <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxScore), $"Maximum score for category '{id}' must be greater than 1.");
            }
            Id = id;
            MaxScore = maxScore;
        }
    }
}
