using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContestJudging.Infrastructure.Repositories
{
    public sealed class SqliteCategoryRepository : ICategoryRepository
    {
        private readonly ContestDbContext _context;
        private readonly ILogger<SqliteCategoryRepository> _logger;

        public SqliteCategoryRepository(ContestDbContext context, ILogger<SqliteCategoryRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Category?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            return entity == null ? null : new Category(entity.Id, entity.MaxScore);
        }

        public async Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Categories.AsNoTracking().ToListAsync(cancellationToken);
            return entities.Select(e => new Category(e.Id, e.MaxScore));
        }

        public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            var entity = new CategoryEntity { Id = category.Id, MaxScore = category.MaxScore };
            await _context.Categories.AddAsync(entity, cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save changes to the database");
                throw;
            }
        }

        public async Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Categories.FindAsync(new object[] { category.Id }, cancellationToken);
            if (entity != null)
            {
                entity.MaxScore = category.MaxScore;
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Failed to save changes to the database");
                    throw;
                }
            }
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Categories.FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                _context.Categories.Remove(entity);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Failed to save changes to the database");
                    throw;
                }
            }
        }
    }

    public sealed class SqliteEntryRepository : IEntryRepository
    {
        private readonly ContestDbContext _context;
        private readonly ILogger<SqliteEntryRepository> _logger;

        public SqliteEntryRepository(ContestDbContext context, ILogger<SqliteEntryRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Entry?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Entries
                .AsNoTracking()
                .Include(e => e.Scores)
                    .ThenInclude(es => es.Category)
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

            if (entity == null) return null;

            var entry = new Entry(entity.Id);
            foreach (var scoreEntity in entity.Scores)
            {
                if (scoreEntity.Category != null)
                {
                    entry.SetScore(new Category(scoreEntity.Category.Id, scoreEntity.Category.MaxScore), scoreEntity.Score);
                }
            }

            return entry;
        }

        public async Task<IEnumerable<Entry>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.Entries
                .AsNoTracking()
                .Include(e => e.Scores)
                    .ThenInclude(es => es.Category)
                .ToListAsync(cancellationToken);

            var entries = new List<Entry>();

            foreach (var entity in entities)
            {
                var entry = new Entry(entity.Id);
                foreach (var scoreEntity in entity.Scores)
                {
                    if (scoreEntity.Category != null)
                    {
                        entry.SetScore(new Category(scoreEntity.Category.Id, scoreEntity.Category.MaxScore), scoreEntity.Score);
                    }
                }
                entries.Add(entry);
            }

            return entries;
        }

        public async Task AddAsync(Entry entry, CancellationToken cancellationToken = default)
        {
            var entity = new EntryEntity { Id = entry.Id };
            foreach (var score in entry.Scores)
            {
                entity.Scores.Add(new EntryScoreEntity
                {
                    EntryId = entry.Id,
                    CategoryId = score.Key,
                    Score = score.Value
                });
            }
            await _context.Entries.AddAsync(entity, cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save changes to the database");
                throw;
            }
        }

        public async Task UpdateAsync(Entry entry, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Entries
                .Include(e => e.Scores)
                .FirstOrDefaultAsync(e => e.Id == entry.Id, cancellationToken);

            if (entity != null)
            {
                _context.EntryScores.RemoveRange(entity.Scores);
                entity.Scores.Clear();

                foreach (var score in entry.Scores)
                {
                    entity.Scores.Add(new EntryScoreEntity
                    {
                        EntryId = entry.Id,
                        CategoryId = score.Key,
                        Score = score.Value
                    });
                }
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Failed to save changes to the database");
                    throw;
                }
            }
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Entries.FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                _context.Entries.Remove(entity);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Failed to save changes to the database");
                    throw;
                }
            }
        }
    }

    public sealed class SqliteRelationRepository : IRelationRepository
    {
        private readonly ContestDbContext _context;
        private readonly ILogger<SqliteRelationRepository> _logger;

        public SqliteRelationRepository(ContestDbContext context, ILogger<SqliteRelationRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Relation>> GetByCategoryIdAsync(string categoryId, CancellationToken cancellationToken = default)
        {
            var categoryEntity = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
            if (categoryEntity == null) return Enumerable.Empty<Relation>();

            var category = new Category(categoryEntity.Id, categoryEntity.MaxScore);
            var entities = await _context.Relations
                .AsNoTracking()
                .Where(r => r.CategoryId == categoryId)
                .ToListAsync(cancellationToken);

            var relations = new List<Relation>();
            foreach (var entity in entities)
            {
                relations.Add(new Relation(
                    category,
                    new Entry(entity.EntryAId),
                    entity.Operator,
                    new Entry(entity.EntryBId)
                ));
            }

            return relations;
        }

        public async Task AddAsync(Relation relation, CancellationToken cancellationToken = default)
        {
            var entity = new RelationEntity
            {
                CategoryId = relation.Category.Id,
                EntryAId = relation.EntryA.Id,
                EntryBId = relation.EntryB.Id,
                Operator = relation.Operator
            };
            await _context.Relations.AddAsync(entity, cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save changes to the database");
                throw;
            }
        }

        public async Task DeleteAsync(string categoryId, string entryAId, string entryBId, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Relations
                .FirstOrDefaultAsync(r => r.CategoryId == categoryId && r.EntryAId == entryAId && r.EntryBId == entryBId, cancellationToken);
            if (entity != null)
            {
                _context.Relations.Remove(entity);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Failed to save changes to the database");
                    throw;
                }
            }
        }
    }
}
