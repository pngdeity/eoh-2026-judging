using System.Collections.Generic;

namespace ContestJudging.Services.Partitioning
{
    public interface IPartitionService
    {
        // Returns a Dictionary mapping Partition IDs to HashSets of Entry IDs
        Dictionary<string, HashSet<string>> GeneratePartitions(
            IEnumerable<string> allEntryIds,
            int kPartitions,
            double overlapRate);
    }
}
