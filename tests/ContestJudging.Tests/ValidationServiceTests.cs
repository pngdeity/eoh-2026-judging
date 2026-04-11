using System.Collections.Generic;

using ContestJudging.Core.Entities;
using ContestJudging.Services.Validation;

using Xunit;

namespace ContestJudging.Tests
{
    public class ValidationServiceTests
    {
        private readonly IValidationService _validationService = new GraphValidationService();

        [Fact]
        public void IsTotalOrder_ValidTotalOrder_ReturnsTrue()
        {
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");

            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, entryB),
                new Relation(cat, entryB, Operator.GreaterThan, entryC)
            };

            var result = _validationService.IsTotalOrder(relations, new[] { "A", "B", "C" });
            Assert.True(result);
        }

        [Fact]
        public void IsTotalOrder_WithTies_ValidTotalOrder_ReturnsTrue()
        {
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");

            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.EqualTo, entryB),
                new Relation(cat, entryA, Operator.GreaterThan, entryC)
            };

            var result = _validationService.IsTotalOrder(relations, new[] { "A", "B", "C" });
            Assert.True(result);
        }

        [Fact]
        public void IsTotalOrder_Cycle_ReturnsFalse()
        {
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");

            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, entryB),
                new Relation(cat, entryB, Operator.GreaterThan, entryC),
                new Relation(cat, entryC, Operator.GreaterThan, entryA)
            };

            var result = _validationService.IsTotalOrder(relations, new[] { "A", "B", "C" });
            Assert.False(result);
        }

        [Fact]
        public void IsTotalOrder_DisconnectedBranches_ReturnsFalse()
        {
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");
            var entryD = new Entry("D");

            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, entryB),
                new Relation(cat, entryC, Operator.GreaterThan, entryD)
            };

            var result = _validationService.IsTotalOrder(relations, new[] { "A", "B", "C", "D" });
            Assert.False(result);
        }

        [Fact]
        public void IsValidOrder_DisconnectedBranches_ReturnsTrue()
        {
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");
            var entryD = new Entry("D");

            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, entryB),
                new Relation(cat, entryC, Operator.GreaterThan, entryD)
            };

            var result = _validationService.IsValidOrder(relations, new[] { "A", "B", "C", "D" });
            Assert.True(result);
        }

        [Fact]
        public void IsValidOrder_Cycle_ReturnsFalse()
        {
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");

            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, entryB),
                new Relation(cat, entryB, Operator.GreaterThan, entryC),
                new Relation(cat, entryC, Operator.GreaterThan, entryA)
            };

            var result = _validationService.IsValidOrder(relations, new[] { "A", "B", "C" });
            Assert.False(result);
        }

        [Fact]
        public void GetSortedTiers_GroupsIncomparableNodes()
        {
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");
            var entryD = new Entry("D");

            // A > B, C > D. A and C are incomparable. B and D are incomparable.
            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, entryB),
                new Relation(cat, entryC, Operator.GreaterThan, entryD)
            };

            var result = _validationService.GetSortedTiers(relations, new[] { "A", "B", "C", "D" });

            // result is lowest to highest.
            // Tier 0 (Lowest): {B, D}
            // Tier 1 (Highest): {A, C}
            Assert.Equal(2, result.Count);
            Assert.True(result[0].Contains("B") && result[0].Contains("D"));
            Assert.True(result[1].Contains("A") && result[1].Contains("C"));
        }

        [Fact]
        public void ValidatePartitionedGraph_DisconnectedGraph_ShouldReturnInvalid()
        {
            // Arrange
            var service = new GraphValidationService();
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");
            var entryD = new Entry("D");
            var allEntryIds = new List<string> { "A", "B", "C", "D" };

            // Two disconnected components: (A>B) and (C>D)
            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, entryB),
                new Relation(cat, entryC, Operator.GreaterThan, entryD)
            };

            // Act
            var result = service.ValidatePartitionedGraph(relations, allEntryIds);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(2, result.ComponentCount);
        }

        [Fact]
        public void ValidatePartitionedGraph_ConnectedGraph_ShouldReturnValid()
        {
            // Arrange
            var service = new GraphValidationService();
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryBridge = new Entry("Bridge");
            var allEntryIds = new List<string> { "A", "B", "Bridge" };

            // One connected component via Bridge: A > Bridge > B
            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, entryBridge),
                new Relation(cat, entryBridge, Operator.GreaterThan, entryB)
            };

            // Act
            var result = service.ValidatePartitionedGraph(relations, allEntryIds);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(1, result.ComponentCount);
        }

        [Fact]
        public void ValidatePartitionedGraph_WithCycles_ShouldReturnInvalid()
        {
            // Arrange
            var service = new GraphValidationService();
            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");
            var allEntryIds = new List<string> { "A", "B", "C" };

            // Cycle: A > B > C > A
            var relations = new List<Relation>
            {
                new Relation(cat, entryA, Operator.GreaterThan, entryB),
                new Relation(cat, entryB, Operator.GreaterThan, entryC),
                new Relation(cat, entryC, Operator.GreaterThan, entryA)
            };

            // Act
            var result = service.ValidatePartitionedGraph(relations, allEntryIds);

            // Assert
            Assert.False(result.IsValid);
        }
    }
}
