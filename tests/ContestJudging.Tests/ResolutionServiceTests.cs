using System;
using System.Collections.Generic;
using System.Linq;

using ContestJudging.Core.Entities;
using ContestJudging.Services.Resolution;

using Xunit;

namespace ContestJudging.Tests
{
    [Trait("Category", "Unit")]
    [Trait("Category", "Unit")]
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

        [Fact]
        public void ResolveGlobalStrengths_LinearOrder_ConvergesWithCorrectOrder()
        {
            var service = new BradleyTerryResolutionService();
            var entries = Enumerable.Range(1, 20).Select(i => new Entry($"E{i}")).ToList();
            var cat = new Category("cat1", 10);
            var allEntryIds = entries.Select(e => e.Id).ToList();
            var relations = new List<Relation>();
            for (int i = 0; i < 19; i++)
                relations.Add(new Relation(cat, entries[i], Operator.GreaterThan, entries[i + 1]));

            var strengths = service.ResolveGlobalStrengths(relations, allEntryIds);

            Assert.Equal(20, strengths.Count);
            for (int i = 0; i < 19; i++)
                Assert.True(strengths[$"E{i + 1}"] > strengths[$"E{i + 2}"], $"E{i + 1} should outrank E{i + 2}");
        }

        [Fact]
        public void ResolveGlobalStrengths_EmptyInput_ReturnsEmpty()
        {
            var service = new BradleyTerryResolutionService();
            var result = service.ResolveGlobalStrengths(Array.Empty<Relation>(), Array.Empty<string>());
            Assert.Empty(result);
        }

        [Fact]
        public void ResolveGlobalStrengths_SingleEntry_ReturnsLogZero()
        {
            var service = new BradleyTerryResolutionService();
            var result = service.ResolveGlobalStrengths(Array.Empty<Relation>(), new[] { "E1" });
            Assert.Single(result);
            Assert.Equal(0.0, result["E1"]);
        }
    }
}
