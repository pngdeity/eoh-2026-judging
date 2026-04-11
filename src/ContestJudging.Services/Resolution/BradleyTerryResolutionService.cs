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

            // Win matrix W[i, j] = number of times i beat j
            // Count matrix N[i, j] = total comparisons between i and j
            double[,] wins = new double[n, n];
            double[,] totalComparisons = new double[n, n];
            double[] totalWins = new double[n];

            foreach (var rel in validRelations)
            {
                int idxA = idToIndex[rel.EntryA.Id];
                int idxB = idToIndex[rel.EntryB.Id];

                if (rel.Operator == Operator.GreaterThan)
                {
                    wins[idxA, idxB]++;
                    totalWins[idxA]++;
                    totalComparisons[idxA, idxB]++;
                    totalComparisons[idxB, idxA]++;
                }
                else if (rel.Operator == Operator.LessThan)
                {
                    wins[idxB, idxA]++;
                    totalWins[idxB]++;
                    totalComparisons[idxA, idxB]++;
                    totalComparisons[idxB, idxA]++;
                }
                else if (rel.Operator == Operator.EqualTo)
                {
                    wins[idxA, idxB] += 0.5;
                    wins[idxB, idxA] += 0.5;
                    totalWins[idxA] += 0.5;
                    totalWins[idxB] += 0.5;
                    totalComparisons[idxA, idxB]++;
                    totalComparisons[idxB, idxA]++;
                }
            }

            // MLE Iterative Scaling (Bradley-Terry)
            double[] gamma = Enumerable.Repeat(1.0, n).ToArray();
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

                // Normalize gamma to prevent overflow/underflow
                double sum = nextGamma.Sum();
                for (int i = 0; i < n; i++) nextGamma[i] /= sum;

                gamma = nextGamma;
                if (maxDiff < ConvergenceThreshold) break;
            }

            var results = new Dictionary<string, double>();
            for (int i = 0; i < n; i++)
            {
                // We use Log(gamma) as the continuous strength metric as it's more standard for BT
                results[allEntryIdsList[i]] = Math.Log(gamma[i]);
            }

            return results;
        }
    }
}
