using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Infrastructure.Persistence;
using ContestJudging.Infrastructure.Repositories;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ContestJudging.Tests
{
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Infrastructure tests require EF Core which is not trimming-safe.")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Integration")]
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
            var logger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var repo = new SqliteCategoryRepository(context, logger);
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
            var catLogger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var entryLogger = Mock.Of<ILogger<SqliteEntryRepository>>();
            var catRepo = new SqliteCategoryRepository(context, catLogger);
            var entryRepo = new SqliteEntryRepository(context, entryLogger);

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
            var catLogger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var entryLogger = Mock.Of<ILogger<SqliteEntryRepository>>();
            var relLogger = Mock.Of<ILogger<SqliteRelationRepository>>();
            var catRepo = new SqliteCategoryRepository(context, catLogger);
            var entryRepo = new SqliteEntryRepository(context, entryLogger);
            var relRepo = new SqliteRelationRepository(context, relLogger);

            var cat = new Category("cat1", 100);
            await catRepo.AddAsync(cat);

            var entryA = new Entry("A");
            var entryB = new Entry("B");
            await entryRepo.AddAsync(entryA);
            await entryRepo.AddAsync(entryB);
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
            var catLogger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var entryLogger = Mock.Of<ILogger<SqliteEntryRepository>>();
            var relLogger = Mock.Of<ILogger<SqliteRelationRepository>>();
            var catRepo = new SqliteCategoryRepository(context, catLogger);
            var entryRepo = new SqliteEntryRepository(context, entryLogger);
            var relRepo = new SqliteRelationRepository(context, relLogger);

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

            Assert.NotEmpty(await relRepo.GetByCategoryIdAsync("cat1"));
            var entryWithScore = await entryRepo.GetByIdAsync("A");
            Assert.NotNull(entryWithScore);
            Assert.True(entryWithScore.Scores.ContainsKey("cat1"));

            await catRepo.DeleteAsync("cat1");

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
            var catLogger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var entryLogger = Mock.Of<ILogger<SqliteEntryRepository>>();
            var relLogger = Mock.Of<ILogger<SqliteRelationRepository>>();
            var catRepo = new SqliteCategoryRepository(context, catLogger);
            var entryRepo = new SqliteEntryRepository(context, entryLogger);
            var relRepo = new SqliteRelationRepository(context, relLogger);

            var cat = new Category("cat1", 100);
            await catRepo.AddAsync(cat);

            var entryA = new Entry("A");
            var entryB = new Entry("B");
            await entryRepo.AddAsync(entryA);
            await entryRepo.AddAsync(entryB);

            var relation = new Relation(cat, entryA, Operator.GreaterThan, entryB);
            await relRepo.AddAsync(relation);

            await entryRepo.DeleteAsync("A");

            Assert.Empty(await relRepo.GetByCategoryIdAsync("cat1"));
            Assert.Null(await entryRepo.GetByIdAsync("A"));
            Assert.NotNull(await entryRepo.GetByIdAsync("B"));
        }

        // TE-006: UpdateAsync/GetAllAsync test coverage

        [Fact]
        public async Task EntryRepository_UpdateAsync_ModifiesEntry()
        {
            using var context = await GetDbContextAsync();
            var catLogger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var entryLogger = Mock.Of<ILogger<SqliteEntryRepository>>();
            var catRepo = new SqliteCategoryRepository(context, catLogger);
            var entryRepo = new SqliteEntryRepository(context, entryLogger);

            var cat = new Category("cat1", 100);
            await catRepo.AddAsync(cat);

            var entry = new Entry("entry1");
            entry.SetScore(cat, 50);
            await entryRepo.AddAsync(entry);

            var updated = new Entry("entry1");
            updated.SetScore(cat, 75);
            await entryRepo.UpdateAsync(updated);

            var result = await entryRepo.GetByIdAsync("entry1");
            Assert.NotNull(result);
            Assert.Equal(75, result.Scores["cat1"]);
        }

        [Fact]
        public async Task CategoryRepository_GetAllAsync_ReturnsAllCategories()
        {
            using var context = await GetDbContextAsync();
            var logger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var repo = new SqliteCategoryRepository(context, logger);

            await repo.AddAsync(new Category("cat1", 100));
            await repo.AddAsync(new Category("cat2", 50));

            var results = (await repo.GetAllAsync()).ToList();
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task EntryRepository_GetAllAsync_ReturnsAllEntries()
        {
            using var context = await GetDbContextAsync();
            var logger = Mock.Of<ILogger<SqliteEntryRepository>>();
            var repo = new SqliteEntryRepository(context, logger);

            await repo.AddAsync(new Entry("entry1"));
            await repo.AddAsync(new Entry("entry2"));

            var results = (await repo.GetAllAsync()).ToList();
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task RelationRepository_GetAllAsync_ReturnsAllRelations()
        {
            using var context = await GetDbContextAsync();
            var catLogger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var entryLogger = Mock.Of<ILogger<SqliteEntryRepository>>();
            var relLogger = Mock.Of<ILogger<SqliteRelationRepository>>();
            var catRepo = new SqliteCategoryRepository(context, catLogger);
            var entryRepo = new SqliteEntryRepository(context, entryLogger);
            var relRepo = new SqliteRelationRepository(context, relLogger);

            var cat = new Category("cat1", 100);
            await catRepo.AddAsync(cat);
            var entryA = new Entry("A");
            var entryB = new Entry("B");
            await entryRepo.AddAsync(entryA);
            await entryRepo.AddAsync(entryB);

            var relation = new Relation(cat, entryA, Operator.GreaterThan, entryB);
            await relRepo.AddAsync(relation);

            var results = (await relRepo.GetByCategoryIdAsync("cat1")).ToList();
            Assert.Single(results);
        }

        // TE-009: Repository edge cases

        [Fact]
        public async Task CategoryRepository_Delete_NonExistent_DoesNotThrow()
        {
            using var context = await GetDbContextAsync();
            var logger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var repo = new SqliteCategoryRepository(context, logger);

            var exception = await Record.ExceptionAsync(() => repo.DeleteAsync("nonexistent"));

            Assert.Null(exception);
        }

        [Fact]
        public async Task CategoryRepository_Add_DuplicateId_ThrowsOrReplaces()
        {
            using var context = await GetDbContextAsync();
            var logger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var repo = new SqliteCategoryRepository(context, logger);

            await repo.AddAsync(new Category("cat1", 100));

            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AddAsync(new Category("cat1", 200)));
        }

        [Fact]
        public async Task EntryRepository_AddWithExistingScores_ReplacesOldScores()
        {
            using var context = await GetDbContextAsync();
            var catLogger = Mock.Of<ILogger<SqliteCategoryRepository>>();
            var entryLogger = Mock.Of<ILogger<SqliteEntryRepository>>();
            var catRepo = new SqliteCategoryRepository(context, catLogger);
            var entryRepo = new SqliteEntryRepository(context, entryLogger);

            var cat = new Category("cat1", 100);
            var cat2 = new Category("cat2", 50);
            await catRepo.AddAsync(cat);
            await catRepo.AddAsync(cat2);

            var entry = new Entry("entry1");
            entry.SetScore(cat, 80);
            entry.SetScore(cat2, 20);
            await entryRepo.AddAsync(entry);

            var updated = new Entry("entry1");
            updated.SetScore(cat, 90);
            await entryRepo.UpdateAsync(updated);

            var result = await entryRepo.GetByIdAsync("entry1");
            Assert.NotNull(result);
            Assert.Equal(90, result.Scores["cat1"]);
            Assert.Single(result.Scores);
            Assert.False(result.Scores.ContainsKey("cat2"));
        }
    }
}
