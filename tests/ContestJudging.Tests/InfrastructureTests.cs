using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Infrastructure.Persistence;
using ContestJudging.Infrastructure.Repositories;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace ContestJudging.Tests
{
    public class InfrastructureTests
    {
        private async Task<ContestDbContext> GetDbContextAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ContestDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ContestDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return context;
        }

        [Fact]
        public async Task CategoryRepository_AddAndGet_Succeeds()
        {
            using var context = await GetDbContextAsync();
            var repo = new SqliteCategoryRepository(context);
            var category = new Category("cat1", 100);

            await repo.AddAsync(category);
            var result = await repo.GetByIdAsync("cat1");

            Assert.NotNull(result);
            Assert.Equal("cat1", result.Id);
            Assert.Equal(100, result.MaxScore);
        }

        [Fact]
        public async Task EntryRepository_AddWithScores_Succeeds()
        {
            using var context = await GetDbContextAsync();
            var catRepo = new SqliteCategoryRepository(context);
            var entryRepo = new SqliteEntryRepository(context);

            var cat = new Category("cat1", 100);
            await catRepo.AddAsync(cat);

            var entry = new Entry("entry1");
            entry.SetScore(cat, 85);

            await entryRepo.AddAsync(entry);
            var result = await entryRepo.GetByIdAsync("entry1");

            Assert.NotNull(result);
            Assert.Equal("entry1", result.Id);
            Assert.Equal(85, result.Scores["cat1"]);
        }

        [Fact]
        public async Task RelationRepository_AddAndGet_Succeeds()
        {
            using var context = await GetDbContextAsync();
            var catRepo = new SqliteCategoryRepository(context);
            var relRepo = new SqliteRelationRepository(context);

            var cat = new Category("cat1", 100);
            await catRepo.AddAsync(cat);

            var entryA = new Entry("A");
            var entryB = new Entry("B");
            var relation = new Relation(cat, entryA, Operator.GreaterThan, entryB);

            await relRepo.AddAsync(relation);
            var results = (await relRepo.GetByCategoryIdAsync("cat1")).ToList();

            Assert.Single(results);
            Assert.Equal("A", results[0].EntryA.Id);
            Assert.Equal("B", results[0].EntryB.Id);
            Assert.Equal(Operator.GreaterThan, results[0].Operator);
        }

        [Fact]
        public async Task CategoryRepository_Delete_Cascades()
        {
            using var context = await GetDbContextAsync();
            var catRepo = new SqliteCategoryRepository(context);
            var entryRepo = new SqliteEntryRepository(context);
            var relRepo = new SqliteRelationRepository(context);

            var cat = new Category("cat1", 100);
            await catRepo.AddAsync(cat);

            var entryA = new Entry("A");
            var entryB = new Entry("B");
            await entryRepo.AddAsync(entryA);
            await entryRepo.AddAsync(entryB);

            var relation = new Relation(cat, entryA, Operator.GreaterThan, entryB);
            await relRepo.AddAsync(relation);

            entryA.SetScore(cat, 50);
            await entryRepo.UpdateAsync(entryA);

            // Verify they exist
            Assert.NotEmpty(await relRepo.GetByCategoryIdAsync("cat1"));
            var entryWithScore = await entryRepo.GetByIdAsync("A");
            Assert.NotNull(entryWithScore);
            Assert.True(entryWithScore.Scores.ContainsKey("cat1"));

            // Delete category
            await catRepo.DeleteAsync("cat1");

            // Verify relations and scores are gone
            Assert.Empty(await relRepo.GetByCategoryIdAsync("cat1"));
            var entryAfterDelete = await entryRepo.GetByIdAsync("A");
            Assert.NotNull(entryAfterDelete);
            Assert.False(entryAfterDelete.Scores.ContainsKey("cat1"));
            Assert.Null(await catRepo.GetByIdAsync("cat1"));
        }

        [Fact]
        public async Task EntryRepository_Delete_Cascades()
        {
            using var context = await GetDbContextAsync();
            var catRepo = new SqliteCategoryRepository(context);
            var entryRepo = new SqliteEntryRepository(context);
            var relRepo = new SqliteRelationRepository(context);

            var cat = new Category("cat1", 100);
            await catRepo.AddAsync(cat);

            var entryA = new Entry("A");
            var entryB = new Entry("B");
            await entryRepo.AddAsync(entryA);
            await entryRepo.AddAsync(entryB);

            var relation = new Relation(cat, entryA, Operator.GreaterThan, entryB);
            await relRepo.AddAsync(relation);

            // Delete entry A
            await entryRepo.DeleteAsync("A");

            // Verify relation is gone
            Assert.Empty(await relRepo.GetByCategoryIdAsync("cat1"));
            Assert.Null(await entryRepo.GetByIdAsync("A"));
            Assert.NotNull(await entryRepo.GetByIdAsync("B"));
        }
    }
}
