using System.Diagnostics.CodeAnalysis;

using Blazored.LocalStorage;

using ContestJudging.Core.Interfaces;
using ContestJudging.Services.Extensions;
using ContestJudging.Services.Managers;
using ContestJudging.Services.Partitioning;
using ContestJudging.Services.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Moq;

using Xunit;

namespace ContestJudging.Web.Tests;

[Trait("Category", "Unit")]
[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "DI registration test requires EF Core which is not trimming-safe.")]
public class ServiceRegistrationTests
{
    [Fact]
    public void AddContestJudgingServices_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddBlazoredLocalStorage();

        services.AddContestJudgingServices();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetService<IContestManager>());
        Assert.NotNull(sp.GetService<IValidationService>());
        Assert.NotNull(sp.GetService<IPartitionService>());
        Assert.NotNull(sp.GetService<IDatabaseBackupService>());
        Assert.NotNull(sp.GetService<IBackupService>());
    }
}
