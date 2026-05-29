using System;
using System.Collections.Generic;
using System.Linq;

using ContestJudging.Core.Entities;

namespace ContestJudging.Web.Services
{
    public static class JudgingUtilities
    {
        public static (string A, string B)? FindSuggestedPair(
            IReadOnlyList<string> entryIds,
            IReadOnlySet<(string, string)> existingPairs)
        {
            if (entryIds.Count < 2) return null;

            for (int i = 0; i < entryIds.Count; i++)
            {
                for (int j = i + 1; j < entryIds.Count; j++)
                {
                    var a = entryIds[i];
                    var b = entryIds[j];
                    if (!existingPairs.Contains((a, b)))
                    {
                        return (a, b);
                    }
                }
            }

            return null;
        }

        public static Operator? MapKeyToOperator(string key)
        {
            return key.ToLower() switch
            {
                "a" or "arrowleft" => Operator.GreaterThan,
                "s" or "arrowdown" => Operator.EqualTo,
                "d" or "arrowright" => Operator.LessThan,
                _ => null
            };
        }

        public static string? ValidateRelationEntries(string? entryAId, string? entryBId)
        {
            if (string.IsNullOrEmpty(entryAId) || string.IsNullOrEmpty(entryBId))
                return "Please select both entries.";
            if (entryAId == entryBId)
                return "Entry A and Entry B must be different.";
            return null;
        }

        public static string GetOperatorText(Operator op)
        {
            return op switch
            {
                Operator.GreaterThan => "is better than ( > )",
                Operator.LessThan => "is worse than ( < )",
                Operator.EqualTo => "is equal to ( = )",
                _ => "unknown"
            };
        }
    }
}
