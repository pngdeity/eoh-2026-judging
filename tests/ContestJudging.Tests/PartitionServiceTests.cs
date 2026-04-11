using System.Collections.Generic;
using System.Linq;

using ContestJudging.Services.Partitioning;

using Xunit;

namespace ContestJudging.Tests
{
    public class PartitionServiceTests
    {
        [Fact]
        public void GeneratePartitions_ShouldMaintainBridgeNodes()
        {
            // Arrange
            var service = new PartitionService();
            var allEntryIds = Enumerable.Range(1, 100).Select(i => i.ToString()).ToList();
            int k = 2;
            double overlap = 0.10; // 10% overlap

            // Act
            var partitions = service.GeneratePartitions(allEntryIds, k, overlap);

            // Assert
            Assert.Equal(k, partitions.Count);

            var partition0 = partitions["0"];
            var partition1 = partitions["1"];

            var common = partition0.Intersect(partition1).ToList();
            Assert.Equal(10, common.Count);

            var allDistinct = partition0.Union(partition1).ToHashSet();
            Assert.Equal(100, allDistinct.Count);
        }

        [Fact]
        public void GeneratePartitions_WithNoOverlap_ShouldHaveDisjointSets()
        {
            // Arrange
            var service = new PartitionService();
            var allEntryIds = Enumerable.Range(1, 10).Select(i => i.ToString()).ToList();
            int k = 2;
            double overlap = 0.0;

            // Act
            var partitions = service.GeneratePartitions(allEntryIds, k, overlap);

            // Assert
            var common = partitions["0"].Intersect(partitions["1"]);
            Assert.Empty(common);
            Assert.Equal(10, partitions["0"].Count + partitions["1"].Count);
        }
    }
}
