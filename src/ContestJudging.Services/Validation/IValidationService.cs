using System.Collections.Generic;

using ContestJudging.Core.Entities;

namespace ContestJudging.Services.Validation
{
    public interface IValidationService
    {
        bool IsTotalOrder(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds);
        bool IsValidOrder(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds);
        List<HashSet<string>> GetSortedTiers(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds);
    }
}
