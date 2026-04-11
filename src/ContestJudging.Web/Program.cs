using Blazored.LocalStorage;
using ContestJudging.Infrastructure.Persistence;
using ContestJudging.Services.Extensions;
using ContestJudging.Services.Managers;
using ContestJudging.Web;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Initialize SQLite
SQLitePCL.Batteries_V2.Init();

// Register LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Register Contest Judging services with in-memory SQLite
builder.Services.AddContestJudgingServices("Data Source=contest.db");

var host = builder.Build();

// Ensure database is created and restored from LocalStorage if available
using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ContestDbContext>();
    var localStorage = scope.ServiceProvider.GetRequiredService<ISyncLocalStorageService>();
    var contestManager = scope.ServiceProvider.GetRequiredService<IContestManager>();
    
    // TRICKY OPTIMIZATION #2: Restore from LocalStorage
    if (localStorage.ContainKey("db_backup"))
    {
        var backupBase64 = localStorage.GetItemAsString("db_backup");
        if (!string.IsNullOrEmpty(backupBase64))
        {
            try 
            {
                var backupBytes = Convert.FromBase64String(backupBase64);
                await contestManager.ImportDataAsync(backupBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to restore database: {ex.Message}");
            }
        }
    }

    await context.Database.EnsureCreatedAsync();
}

await host.RunAsync();
