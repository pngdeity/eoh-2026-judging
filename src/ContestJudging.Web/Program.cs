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

// Register Contest Judging services with in-memory SQLite
builder.Services.AddContestJudgingServices("Data Source=contest.db");

var host = builder.Build();

// Ensure database is created (in-memory)
using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ContestDbContext>();
    await context.Database.EnsureCreatedAsync();
}

await host.RunAsync();
