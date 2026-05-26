using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using ContestJudging.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace ContestJudging.Infrastructure.Persistence
{
    public class CategoryEntity
    {
        public string Id { get; set; } = string.Empty;
        public double MaxScore { get; set; }
        public List<EntryScoreEntity> Scores { get; set; } = new();
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
        public CategoryEntity Category { get; set; } = null!;
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
            modelBuilder.Entity<CategoryEntity>().Property(c => c.Id).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<EntryEntity>().HasKey(e => e.Id);
            modelBuilder.Entity<EntryEntity>().Property(e => e.Id).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<RelationEntity>().HasKey(r => r.Id);
            modelBuilder.Entity<RelationEntity>().Property(r => r.CategoryId).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<RelationEntity>().Property(r => r.EntryAId).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<RelationEntity>().Property(r => r.EntryBId).HasMaxLength(100).IsRequired();

            modelBuilder.Entity<EntryScoreEntity>().HasKey(es => es.Id);
            modelBuilder.Entity<EntryScoreEntity>().Property(es => es.EntryId).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<EntryScoreEntity>().Property(es => es.CategoryId).HasMaxLength(100).IsRequired();

            modelBuilder.Entity<EntryScoreEntity>()
                .HasIndex(es => new { es.EntryId, es.CategoryId })
                .IsUnique();

            modelBuilder.Entity<EntryScoreEntity>()
                .HasOne<EntryEntity>()
                .WithMany(e => e.Scores)
                .HasForeignKey(es => es.EntryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EntryScoreEntity>()
                .HasOne(es => es.Category)
                .WithMany(c => c.Scores)
                .HasForeignKey(es => es.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RelationEntity>()
                .HasOne<CategoryEntity>()
                .WithMany()
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RelationEntity>()
                .HasOne<EntryEntity>()
                .WithMany()
                .HasForeignKey(r => r.EntryAId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RelationEntity>()
                .HasOne<EntryEntity>()
                .WithMany()
                .HasForeignKey(r => r.EntryBId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RelationEntity>().HasIndex(r => r.CategoryId);
            modelBuilder.Entity<RelationEntity>().HasIndex(r => r.EntryAId);
            modelBuilder.Entity<RelationEntity>().HasIndex(r => r.EntryBId);
            modelBuilder.Entity<EntryScoreEntity>().HasIndex(es => es.EntryId);
        }
    }
}
