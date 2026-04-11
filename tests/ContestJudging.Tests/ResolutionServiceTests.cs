using System.Collections.Generic;

using ContestJudging.Core.Entities;
using ContestJudging.Services.Resolution;

using Xunit;

namespace ContestJudging.Tests
{
    public class ResolutionServiceTests
    {
        [Fact]
        public void ResolveGlobalStrengths_ShouldProduceTransitiveRanks()
        {
            // Arrange
            var service = new BradleyTerryResolutionService();
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var bridge = new Entry("Bridge");

            var allEntryIds = new List<string> { "A", "B", "Bridge" };

            // A beats Bridge, Bridge beats B
            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, bridge),
                new Relation(cat, bridge, Operator.GreaterThan, entryB)
            };

            // Act
            var strengths = service.ResolveGlobalStrengths(relations, allEntryIds);

            // Assert
            Assert.True(strengths["A"] > strengths["Bridge"]);
            Assert.True(strengths["Bridge"] > strengths["B"]);
            Assert.True(strengths["A"] > strengths["B"]);
        }

        [Fact]
        public void ResolveGlobalStrengths_WithEqualRelations_ShouldProduceEqualStrengths()
        {
            // Arrange
            var service = new BradleyTerryResolutionService();
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var allEntryIds = new List<string> { "A", "B" };

            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.EqualTo, entryB)
            };

            // Act
            var strengths = service.ResolveGlobalStrengths(relations, allEntryIds);

            // Assert
            Assert.Equal(strengths["A"], strengths["B"], 5);
        }
    }
}
