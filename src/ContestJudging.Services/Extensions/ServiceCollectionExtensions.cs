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

namespace ContestJudging.Services.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddContestJudgingServices(this IServiceCollection services, string connectionString = "Data Source=:memory:")
        {
            services.AddDbContext<ContestDbContext>(options =>
                options.UseSqlite(connectionString));

            services.AddScoped<ICategoryRepository, SqliteCategoryRepository>();
            services.AddScoped<IEntryRepository, SqliteEntryRepository>();
            services.AddScoped<IRelationRepository, SqliteRelationRepository>();

            services.AddScoped<IValidationService, GraphValidationService>();
            services.AddScoped<IPartitionService, PartitionService>();
            services.AddScoped<IGlobalRankingService, BradleyTerryResolutionService>();
            services.AddScoped<IScoringStrategy, LinearSpacingScoring>();
            services.AddScoped<IContestManager, ContestManager>();

            return services;
        }
    }
}
