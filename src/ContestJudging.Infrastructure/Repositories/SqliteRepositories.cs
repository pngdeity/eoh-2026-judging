using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace ContestJudging.Infrastructure.Repositories
{
    public class SqliteCategoryRepository : ICategoryRepository
    {
        private readonly ContestDbContext _context;

        public SqliteCategoryRepository(ContestDbContext context)
        {
            _context = context;
        }

        public async Task<Category?> GetByIdAsync(string id)
        {
            var entity = await _context.Categories.FindAsync(id);
            return entity == null ? null : new Category(entity.Id, entity.MaxScore);
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            var entities = await _context.Categories.ToListAsync();
            return entities.Select(e => new Category(e.Id, e.MaxScore));
        }

        public async Task AddAsync(Category category)
        {
            var entity = new CategoryEntity { Id = category.Id, MaxScore = category.MaxScore };
            await _context.Categories.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            var entity = await _context.Categories.FindAsync(category.Id);
            if (entity != null)
            {
                entity.MaxScore = category.MaxScore;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(string id)
        {
            var entity = await _context.Categories.FindAsync(id);
            if (entity != null)
            {
                // Cascade delete manually
                var relations = await _context.Relations.Where(r => r.CategoryId == id).ToListAsync();
                _context.Relations.RemoveRange(relations);

                var scores = await _context.EntryScores.Where(es => es.CategoryId == id).ToListAsync();
                _context.EntryScores.RemoveRange(scores);

                _context.Categories.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }

    public class SqliteEntryRepository : IEntryRepository
    {
        private readonly ContestDbContext _context;

        public SqliteEntryRepository(ContestDbContext context)
        {
            _context = context;
        }

        public async Task<Entry?> GetByIdAsync(string id)
        {
            var entity = await _context.Entries
                .Include(e => e.Scores)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entity == null) return null;

            var entry = new Entry(entity.Id);
            var categories = await _context.Categories.ToListAsync();

            foreach (var scoreEntity in entity.Scores)
            {
                var categoryEntity = categories.FirstOrDefault(c => c.Id == scoreEntity.CategoryId);
                if (categoryEntity != null)
                {
                    entry.SetScore(new Category(categoryEntity.Id, categoryEntity.MaxScore), scoreEntity.Score);
                }
            }

            return entry;
        }

        public async Task<IEnumerable<Entry>> GetAllAsync()
        {
            var entities = await _context.Entries
                .Include(e => e.Scores)
                .ToListAsync();

            var categories = await _context.Categories.ToListAsync();
            var entries = new List<Entry>();

            foreach (var entity in entities)
            {
                var entry = new Entry(entity.Id);
                foreach (var scoreEntity in entity.Scores)
                {
                    var categoryEntity = categories.FirstOrDefault(c => c.Id == scoreEntity.CategoryId);
                    if (categoryEntity != null)
                    {
                        entry.SetScore(new Category(categoryEntity.Id, categoryEntity.MaxScore), scoreEntity.Score);
                    }
                }
                entries.Add(entry);
            }

            return entries;
        }

        public async Task AddAsync(Entry entry)
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
            await _context.Entries.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Entry entry)
        {
            var entity = await _context.Entries
                .Include(e => e.Scores)
                .FirstOrDefaultAsync(e => e.Id == entry.Id);

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
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(string id)
        {
            var entity = await _context.Entries.FindAsync(id);
            if (entity != null)
            {
                // Cascade delete manually
                var relations = await _context.Relations.Where(r => r.EntryAId == id || r.EntryBId == id).ToListAsync();
                _context.Relations.RemoveRange(relations);

                var scores = await _context.EntryScores.Where(es => es.EntryId == id).ToListAsync();
                _context.EntryScores.RemoveRange(scores);

                _context.Entries.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }

    public class SqliteRelationRepository : IRelationRepository
    {
        private readonly ContestDbContext _context;

        public SqliteRelationRepository(ContestDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Relation>> GetByCategoryIdAsync(string categoryId)
        {
            var categoryEntity = await _context.Categories.FindAsync(categoryId);
            if (categoryEntity == null) return Enumerable.Empty<Relation>();

            var category = new Category(categoryEntity.Id, categoryEntity.MaxScore);
            var entities = await _context.Relations
                .Where(r => r.CategoryId == categoryId)
                .ToListAsync();

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

        public async Task AddAsync(Relation relation)
        {
            var entity = new RelationEntity
            {
                CategoryId = relation.Category.Id,
                EntryAId = relation.EntryA.Id,
                EntryBId = relation.EntryB.Id,
                Operator = relation.Operator
            };
            await _context.Relations.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(string categoryId, string entryAId, string entryBId)
        {
            var entity = await _context.Relations
                .FirstOrDefaultAsync(r => r.CategoryId == categoryId && r.EntryAId == entryAId && r.EntryBId == entryBId);
            if (entity != null)
            {
                _context.Relations.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
