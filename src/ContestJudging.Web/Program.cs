using System.Diagnostics.CodeAnalysis;
using System.IO;

using Blazored.LocalStorage;

using ContestJudging.Infrastructure.Persistence;
using ContestJudging.Services.Extensions;
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
AddServices(builder.Services);

var host = builder.Build();

// Restore database from LocalStorage BEFORE creating DbContext scope.
// This avoids SQLite seeing an empty database cached by the connection.
var localStorage = host.Services.GetRequiredService<ILocalStorageService>();
if (await localStorage.ContainKeyAsync("db_backup"))
{
    var backupBase64 = await localStorage.GetItemAsStringAsync("db_backup");
    if (!string.IsNullOrEmpty(backupBase64))
    {
        try
        {
            var backupBytes = Convert.FromBase64String(backupBase64);
            await File.WriteAllBytesAsync("contest.db", backupBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restore database from backup: {ex.Message}");
        }
    }
}

using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ContestDbContext>();
    await context.Database.EnsureCreatedAsync();
}

await host.RunAsync();

// Local functions are allowed at the end of top-level statements, 
// but we must ensure no other code follows them.
[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "EF Core initialization is required at startup. Risk is mitigated by TrimmingSafetyTests.")]
static void AddServices(IServiceCollection services)
{
    services.AddContestJudgingServices("Data Source=contest.db");
}
