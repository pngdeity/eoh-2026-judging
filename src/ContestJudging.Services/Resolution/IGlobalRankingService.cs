using System.Collections.Generic;

using ContestJudging.Core.Entities;

namespace ContestJudging.Services.Resolution
{
    public interface IGlobalRankingService
    {
        // Takes the validated relations and outputs a Dictionary mapping EntryID to a continuous strength/rank value.
        Dictionary<string, double> ResolveGlobalStrengths(IEnumerable<Relation> validRelations, IEnumerable<string> allEntryIds);
    }
}
