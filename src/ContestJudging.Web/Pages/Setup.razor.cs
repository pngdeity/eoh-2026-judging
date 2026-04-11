using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Services.Partitioning;

using Microsoft.AspNetCore.Components;

namespace ContestJudging.Web.Pages
{
    public partial class Setup
    {
        [Inject] private ICategoryRepository CategoryRepository { get; set; } = default!;
        [Inject] private IEntryRepository EntryRepository { get; set; } = default!;
        [Inject] private IPartitionService PartitionService { get; set; } = default!;

        private List<Category> categories = new();
        private List<Entry> entries = new();

        private CategoryModel newCategory = new();
        private EntryModel newEntry = new();
        private string bulkEntriesText = "";

        // Partitioning State
        private int kPartitions = 2;
        private double overlapRate = 0.1;
        private Dictionary<string, HashSet<string>>? generatedPartitions;

        protected override async Task OnInitializedAsync()
        {
            await RefreshData();
        }

        private async Task RefreshData()
        {
            categories = (await CategoryRepository.GetAllAsync()).ToList();
            entries = (await EntryRepository.GetAllAsync()).ToList();
        }

        private async Task ClearCategories()
        {
            foreach (var cat in categories.ToList())
            {
                await CategoryRepository.DeleteAsync(cat.Id);
            }
            await RefreshData();
        }

        private async Task ClearEntries()
        {
            foreach (var entry in entries.ToList())
            {
                await EntryRepository.DeleteAsync(entry.Id);
            }
            await RefreshData();
        }

        private async Task BulkImportEntries()
        {
            if (string.IsNullOrWhiteSpace(bulkEntriesText)) return;

            var newEntries = bulkEntriesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct();

            foreach (var entryId in newEntries)
            {
                if (!entries.Any(e => e.Id == entryId))
                {
                    await EntryRepository.AddAsync(new Entry(entryId));
                }
            }

            bulkEntriesText = "";
            await RefreshData();
        }

        private async Task AddCategory()
        {
            if (string.IsNullOrWhiteSpace(newCategory.Id)) return;

            var category = new Category(newCategory.Id, newCategory.MaxScore);
            await CategoryRepository.AddAsync(category);
            newCategory = new();
            await RefreshData();
        }

        private async Task DeleteCategory(string id)
        {
            await CategoryRepository.DeleteAsync(id);
            await RefreshData();
        }

        private async Task AddEntry()
        {
            if (string.IsNullOrWhiteSpace(newEntry.Id)) return;

            var entry = new Entry(newEntry.Id);
            await EntryRepository.AddAsync(entry);
            newEntry = new();
            await RefreshData();
        }

        private async Task DeleteEntry(string id)
        {
            await EntryRepository.DeleteAsync(id);
            await RefreshData();
        }

        private void GeneratePartitions()
        {
            if (entries.Count < 2) return;
            generatedPartitions = PartitionService.GeneratePartitions(entries.Select(e => e.Id), kPartitions, overlapRate);
        }

        public class CategoryModel
        {
            [Required]
            public string Id { get; set; } = string.Empty;
            [Range(1.1, double.MaxValue)]
            public double MaxScore { get; set; } = 10;
        }

        public class EntryModel
        {
            [Required]
            public string Id { get; set; } = string.Empty;
        }
    }
}
