using System;
using System.Collections.Generic;
using System.Linq;

using ContestJudging.Core.Entities;

namespace ContestJudging.Services.Resolution
{
    public class BradleyTerryResolutionService : IGlobalRankingService
    {
        private const int MaxIterations = 1000;
        private const double ConvergenceThreshold = 1e-6;

        public Dictionary<string, double> ResolveGlobalStrengths(IEnumerable<Relation> validRelations, IEnumerable<string> allEntryIds)
        {
            var allEntryIdsList = allEntryIds.ToList();
            int n = allEntryIdsList.Count;
            if (n == 0) return new Dictionary<string, double>();

            var idToIndex = allEntryIdsList.Select((id, index) => new { id, index })
                                          .ToDictionary(x => x.id, x => x.index);

            double[,] totalComparisons = new double[n, n];
            double[] totalWins = new double[n];

            foreach (var rel in validRelations)
            {
                int idxA = idToIndex[rel.EntryA.Id];
                int idxB = idToIndex[rel.EntryB.Id];

                if (rel.Operator == Operator.GreaterThan)
                {
                    totalWins[idxA]++;
                    totalComparisons[idxA, idxB]++;
                    totalComparisons[idxB, idxA]++;
                }
                else if (rel.Operator == Operator.LessThan)
                {
                    totalWins[idxB]++;
                    totalComparisons[idxA, idxB]++;
                    totalComparisons[idxB, idxA]++;
                }
                else if (rel.Operator == Operator.EqualTo)
                {
                    totalWins[idxA] += 0.5;
                    totalWins[idxB] += 0.5;
                    totalComparisons[idxA, idxB]++;
                    totalComparisons[idxB, idxA]++;
                }
            }

            // MLE Iterative Scaling (Bradley-Terry)
            double[] gamma = Enumerable.Repeat(1.0, n).ToArray();
            int[] lastRanks = new int[n];

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                double[] nextGamma = new double[n];
                double maxDiff = 0;

                for (int i = 0; i < n; i++)
                {
                    double denominator = 0;
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) continue;
                        if (totalComparisons[i, j] > 0)
                        {
                            denominator += totalComparisons[i, j] / (gamma[i] + gamma[j]);
                        }
                    }

                    if (denominator > 0)
                    {
                        nextGamma[i] = totalWins[i] / denominator;
                    }
                    else
                    {
                        nextGamma[i] = gamma[i];
                    }
                    
                    maxDiff = Math.Max(maxDiff, Math.Abs(nextGamma[i] - gamma[i]));
                }

                // Normalize gamma
                double sum = nextGamma.Sum();
                for (int i = 0; i < n; i++) nextGamma[i] /= sum;

                // TRICKY OPTIMIZATION #4: MLE Early-Exit based on Rank Stability
                if (iter > 50 && iter % 10 == 0)
                {
                    var currentRanks = nextGamma
                        .Select((val, idx) => new { val, idx })
                        .OrderByDescending(x => x.val)
                        .Select((x, rank) => new { x.idx, rank })
                        .ToDictionary(x => x.idx, x => x.rank);

                    bool stable = true;
                    for (int i = 0; i < n; i++)
                    {
                        if (currentRanks[i] != lastRanks[i])
                        {
                            stable = false;
                            break;
                        }
                    }

                    if (stable && maxDiff < 1e-3) 
                    {
                        gamma = nextGamma;
                        break; 
                    }

                    for (int i = 0; i < n; i++) lastRanks[i] = currentRanks[i];
                }

                gamma = nextGamma;
                if (maxDiff < ConvergenceThreshold) break;
            }

            var results = new Dictionary<string, double>();
            for (int i = 0; i < n; i++)
            {
                results[allEntryIdsList[i]] = Math.Log(gamma[i]);
            }

            return results;
        }
    }
}
