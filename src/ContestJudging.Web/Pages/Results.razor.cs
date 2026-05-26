using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Services.Managers;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace ContestJudging.Web.Pages
{
    public partial class Results
    {
        [Inject] private ICategoryRepository CategoryRepository { get; set; } = default!;
        [Inject] private IEntryRepository EntryRepository { get; set; } = default!;
        [Inject] private IContestManager ContestManager { get; set; } = default!;
        [Inject] private IBackupService BackupService { get; set; } = default!;
        [Inject] private ILogger<Results> Logger { get; set; } = default!;

        private List<Category> categories = new();
        private List<Entry> entries = new();
        private List<string> validationErrors = new();
        private List<LeaderboardItem> leaderboard = new();
        private string errorMessage = "";

        protected override async Task OnInitializedAsync()
        {
            try
            {
                categories = (await CategoryRepository.GetAllAsync()).ToList();
                entries = (await EntryRepository.GetAllAsync()).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load categories or entries on results page");
                errorMessage = "Failed to load data. Please refresh the page or contact support.";
            }
        }

        private async Task CalculateResults()
        {
            errorMessage = "";
            validationErrors.Clear();
            leaderboard.Clear();

            if (!categories.Any())
            {
                validationErrors.Add("No categories defined. Please go to Setup.");
                return;
            }

            bool anyError = false;
            foreach (var cat in categories)
            {
                try
                {
                    var result = await ContestManager.CalculateGlobalScoresAsync(cat.Id, cat.MaxScore);

                    if (!result.IsValid)
                    {
                        validationErrors.Add($"Category '{cat.Id}': {result.ErrorMessage}");
                        if (result.ErrorMessage.Contains("cycles")) anyError = true;
                    }
                    else if (result.ComponentCount > 1)
                    {
                        validationErrors.Add($"Category '{cat.Id}' warning: Graph has {result.ComponentCount} disconnected components.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to calculate scores for category {CategoryId}", cat.Id);
                    errorMessage = $"Failed to calculate results for category '{cat.Id}'. Please try again.";
                    return;
                }
            }

            if (anyError) return;

            try
            {
                var allEntries = (await EntryRepository.GetAllAsync()).ToList();
                leaderboard = allEntries
                    .Select(e => new LeaderboardItem { Entry = e })
                    .OrderByDescending(i => i.Entry.TotalScore)
                    .ToList();

                for (int i = 0; i < leaderboard.Count; i++)
                {
                    leaderboard[i].Rank = i + 1;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load entries for leaderboard");
                errorMessage = "Failed to load leaderboard data. Please try again.";
            }

            await BackupDatabase();
        }

        private async Task BackupDatabase()
        {
            var data = await ContestManager.ExportDataAsync();
            if (data.Length > 0)
            {
                await BackupService.SaveBackupAsync(data);
            }
        }

        public class LeaderboardItem
        {
            public Entry Entry { get; set; } = default!;
            public int Rank { get; set; }
        }
    }
}
