using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Services.Managers;

using Microsoft.AspNetCore.Components;

namespace ContestJudging.Web.Pages
{
    public partial class Results
    {
        [Inject] private ICategoryRepository CategoryRepository { get; set; } = default!;
        [Inject] private IEntryRepository EntryRepository { get; set; } = default!;
        [Inject] private IContestManager ContestManager { get; set; } = default!;

        private List<Category> categories = new();
        private List<Entry> entries = new();
        private List<string> validationErrors = new();
        private List<LeaderboardItem> leaderboard = new();

        protected override async Task OnInitializedAsync()
        {
            categories = (await CategoryRepository.GetAllAsync()).ToList();
            entries = (await EntryRepository.GetAllAsync()).ToList();
        }

        private async Task CalculateResults()
        {
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
                // We use the new consolidated manager method
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

            if (anyError) return;

            // Refresh entries with new scores
            var allEntries = (await EntryRepository.GetAllAsync()).ToList();
            leaderboard = allEntries
                .Select(e => new LeaderboardItem { Entry = e })
                .OrderByDescending(i => i.Entry.TotalScore)
                .ToList();
        }

        public class LeaderboardItem
        {
            public Entry Entry { get; set; } = default!;
        }
    }
}
