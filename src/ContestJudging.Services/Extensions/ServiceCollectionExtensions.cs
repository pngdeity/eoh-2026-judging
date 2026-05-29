using System.Diagnostics.CodeAnalysis;

using ContestJudging.Core.Interfaces;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Infrastructure.Persistence;
using ContestJudging.Infrastructure.Repositories;
using ContestJudging.Services.Managers;
using ContestJudging.Services.Partitioning;
using ContestJudging.Services.Resolution;
using ContestJudging.Services.Scoring;
using ContestJudging.Services.Validation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SQLitePCL;

namespace ContestJudging.Services.Extensions
{
    public static class ServiceCollectionExtensions
    {
        [RequiresUnreferencedCode("EF Core is not trimming-safe.")]
        public static IServiceCollection AddContestJudgingServices(this IServiceCollection services, string connectionString = "Data Source=:memory:")
        {
            Batteries_V2.Init();

            services.AddDbContext<ContestDbContext>(options =>
                options.UseSqlite(connectionString));

            services.AddScoped<ICategoryRepository, SqliteCategoryRepository>();
            services.AddScoped<IEntryRepository, SqliteEntryRepository>();
            services.AddScoped<IRelationRepository, SqliteRelationRepository>();

            services.AddScoped<IValidationService, GraphValidationService>();
            services.AddScoped<IPartitionService, PartitionService>();
            services.AddScoped<IGlobalRankingService, BradleyTerryResolutionService>();
            services.AddScoped<IScoringStrategy, LinearSpacingScoring>();
            services.AddScoped<IDatabaseBackupService>(sp =>
                // Database path is hardcoded — client-side WASM app with no config file support.
                // SQLite is embedded in the browser; the path is safe.
                new DatabaseBackupService("contest.db"));
            services.AddScoped<IBackupService, BackupService>();
            services.AddScoped<IContestManager, ContestManager>();

            services.AddScoped<PercentileScoring>();
            services.AddScoped<DefinedIntervalScoring>();

            return services;
        }
    }
}
