using System;
using System.Diagnostics.CodeAnalysis;

using Blazored.LocalStorage;

using ContestJudging.Core.Interfaces;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Services.Extensions;
using ContestJudging.Services.Managers;
using ContestJudging.Services.Partitioning;
using ContestJudging.Services.Resolution;
using ContestJudging.Services.Scoring;
using ContestJudging.Services.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ContestJudging.Tests
{
    [Trait("Category", "Unit")]
    public class ServiceRegistrationTests
    {
        [Fact]
        [RequiresUnreferencedCode("EF Core is not trimming-safe.")]
        public void AddContestJudgingServices_AllCoreRegistrations_CanBeResolved()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(new Mock<ILocalStorageService>().Object);
            services.AddContestJudgingServices();

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<ICategoryRepository>());
            Assert.NotNull(provider.GetService<IEntryRepository>());
            Assert.NotNull(provider.GetService<IRelationRepository>());
            Assert.NotNull(provider.GetService<IValidationService>());
            Assert.NotNull(provider.GetService<IPartitionService>());
            Assert.NotNull(provider.GetService<IGlobalRankingService>());
            Assert.NotNull(provider.GetService<IScoringStrategy>());
            Assert.NotNull(provider.GetService<IDatabaseBackupService>());
            Assert.NotNull(provider.GetService<IBackupService>());
            Assert.NotNull(provider.GetService<IContestManager>());
            Assert.NotNull(provider.GetService<PercentileScoring>());
            Assert.NotNull(provider.GetService<DefinedIntervalScoring>());
        }
    }
}
