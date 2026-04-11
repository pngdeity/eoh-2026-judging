using System.Collections.Generic;

using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Services.Managers;
using ContestJudging.Services.Validation;

using Moq;

using Xunit;

namespace ContestJudging.Tests
{
    public class ContestManagerTests
    {
        [Fact]
        public async Task ValidateCategoryRelations_FlagsMissingEntries()
        {
            var mockCatRepo = new Mock<ICategoryRepository>();
            var mockEntryRepo = new Mock<IEntryRepository>();
            var mockRelRepo = new Mock<IRelationRepository>();
            var mockValidation = new Mock<IValidationService>();

            var manager = new ContestManager(mockCatRepo.Object, mockEntryRepo.Object, mockRelRepo.Object, mockValidation.Object);

            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var entryC = new Entry("C");

            var relation1 = new Relation(cat, entryA, Operator.GreaterThan, entryB);

            mockRelRepo.Setup(r => r.GetByCategoryIdAsync("cat1"))
                .ReturnsAsync(new List<Relation> { relation1 });
            mockEntryRepo.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Entry> { entryA, entryB, entryC });

            // entryC is missing from relations
            Assert.False(await manager.ValidateCategoryRelationsAsync("cat1"));

            var relation2 = new Relation(cat, entryB, Operator.GreaterThan, entryC);
            mockRelRepo.Setup(r => r.GetByCategoryIdAsync("cat1"))
                .ReturnsAsync(new List<Relation> { relation1, relation2 });

            Assert.True(await manager.ValidateCategoryRelationsAsync("cat1"));
        }

        [Fact]
        public async Task CheckTotalOrder_CallsValidationService()
        {
            var mockCatRepo = new Mock<ICategoryRepository>();
            var mockEntryRepo = new Mock<IEntryRepository>();
            var mockRelRepo = new Mock<IRelationRepository>();
            var mockValidation = new Mock<IValidationService>();

            var manager = new ContestManager(mockCatRepo.Object, mockEntryRepo.Object, mockRelRepo.Object, mockValidation.Object);

            var cat = new Category("cat1", 10);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var relation = new Relation(cat, entryA, Operator.GreaterThan, entryB);

            mockRelRepo.Setup(r => r.GetByCategoryIdAsync("cat1"))
                .ReturnsAsync(new List<Relation> { relation });
            mockEntryRepo.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Entry> { entryA, entryB });

            mockValidation.Setup(v => v.IsTotalOrder(
                It.IsAny<IEnumerable<Relation>>(),
                It.IsAny<IEnumerable<string>>()
            )).Returns(true);

            var result = await manager.CheckTotalOrderAsync("cat1");

            Assert.True(result);
            mockValidation.Verify(v => v.IsTotalOrder(
                It.Is<IEnumerable<Relation>>(r => r.Contains(relation)),
                It.Is<IEnumerable<string>>(s => s.Contains("A") && s.Contains("B"))
            ), Times.Once);
        }
    }
}
