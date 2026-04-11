using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace ContestJudging.Infrastructure.Persistence
{
    public class CategoryEntity
    {
        public string Id { get; set; } = string.Empty;
        public double MaxScore { get; set; }
    }

    public class EntryEntity
    {
        public string Id { get; set; } = string.Empty;
        public List<EntryScoreEntity> Scores { get; set; } = new();
    }

    public class EntryScoreEntity
    {
        public int Id { get; set; }
        public string EntryId { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public double Score { get; set; }
    }

    public class RelationEntity
    {
        public int Id { get; set; }
        public string CategoryId { get; set; } = string.Empty;
        public string EntryAId { get; set; } = string.Empty;
        public string EntryBId { get; set; } = string.Empty;
        public Operator Operator { get; set; }
    }

    public class ContestDbContext : DbContext
    {
        public DbSet<CategoryEntity> Categories { get; set; } = null!;
        public DbSet<EntryEntity> Entries { get; set; } = null!;
        public DbSet<RelationEntity> Relations { get; set; } = null!;
        public DbSet<EntryScoreEntity> EntryScores { get; set; } = null!;

        [RequiresUnreferencedCode("EF Core is not trimming-safe.")]
        public ContestDbContext(DbContextOptions<ContestDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CategoryEntity>().HasKey(c => c.Id);
            modelBuilder.Entity<EntryEntity>().HasKey(e => e.Id);
            modelBuilder.Entity<RelationEntity>().HasKey(r => r.Id);

            modelBuilder.Entity<EntryScoreEntity>().HasKey(es => es.Id);
            modelBuilder.Entity<EntryScoreEntity>()
                .HasIndex(es => new { es.EntryId, es.CategoryId })
                .IsUnique();
        }

        // TRICKY OPTIMIZATION #2: Database Export
        public async Task<byte[]> ExportDatabaseAsync()
        {
            var path = "contest.db";
            if (File.Exists(path))
            {
                return await File.ReadAllBytesAsync(path);
            }
            return Array.Empty<byte>();
        }

        public async Task ImportDatabaseAsync(byte[] data)
        {
            var path = "contest.db";
            await File.WriteAllBytesAsync(path, data);
        }
    }
}
