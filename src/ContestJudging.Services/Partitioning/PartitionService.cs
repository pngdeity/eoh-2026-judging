using System;
using System.Collections.Generic;
using System.Linq;

namespace ContestJudging.Services.Partitioning
{
    public sealed class PartitionService : IPartitionService
    {
        // System.Random is acceptable here — used only for partition shuffling,
        // not for cryptographic purposes. Seeded for test determinism.
        private readonly Random _random;

        public PartitionService() : this(Random.Shared) { }

        public PartitionService(Random random)
        {
            _random = random;
        }

        public Dictionary<string, HashSet<string>> GeneratePartitions(
            IEnumerable<string> allEntryIds,
            int kPartitions,
            double overlapRate)
        {
            if (kPartitions <= 0) throw new ArgumentException("kPartitions must be greater than 0", nameof(kPartitions));
            if (overlapRate < 0 || overlapRate > 1) throw new ArgumentException("overlapRate must be between 0 and 1", nameof(overlapRate));

            var allEntryIdsList = allEntryIds.ToList();
            int n = allEntryIdsList.Count;
            int bCount = overlapRate > 0
                ? Math.Max(1, (int)Math.Round(n * overlapRate))
                : 0;

            // Shuffling to select random bridge nodes
            var shuffled = allEntryIdsList.OrderBy(x => _random.Next()).ToList();
            var bridgeNodes = shuffled.Take(bCount).ToHashSet();
            var uniqueNodes = shuffled.Skip(bCount).ToList();

            var partitions = new Dictionary<string, HashSet<string>>();
            for (int i = 0; i < kPartitions; i++)
            {
                partitions[i.ToString()] = new HashSet<string>(bridgeNodes);
            }

            for (int i = 0; i < uniqueNodes.Count; i++)
            {
                int partitionIndex = i % kPartitions;
                partitions[partitionIndex.ToString()].Add(uniqueNodes[i]);
            }

            return partitions;
        }
    }
}
