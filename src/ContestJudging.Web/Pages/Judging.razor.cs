using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Services.Managers;
using ContestJudging.Services.Partitioning;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace ContestJudging.Web.Pages
{
    public partial class Judging : IAsyncDisposable
    {
        [Inject] private ICategoryRepository CategoryRepository { get; set; } = default!;
        [Inject] private IEntryRepository EntryRepository { get; set; } = default!;
        [Inject] private IRelationRepository RelationRepository { get; set; } = default!;
        [Inject] private IPartitionService PartitionService { get; set; } = default!;
        [Inject] private IContestManager ContestManager { get; set; } = default!;
        [Inject] private IBackupService BackupService { get; set; } = default!;
        [Inject] private ILogger<Judging> Logger { get; set; } = default!;

        private List<Category> categories = new();
        private List<Entry> entries = new();
        private List<Relation> relations = new();

        private Category? selectedCategory;
        private string entryAId = "";
        private string entryBId = "";
        private Operator op = Operator.GreaterThan;
        private string errorMessage = "";
        private bool showManualOverride;
        private (string A, string B)? suggestedPair;

        private int kPartitions = 1;
        private double overlapRate = 0.1;
        private Dictionary<string, HashSet<string>>? currentPartitions;
        private string? selectedPartitionId;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                categories = (await CategoryRepository.GetAllAsync()).ToList();
                entries = (await EntryRepository.GetAllAsync()).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize {Page}", GetType().Name);
                errorMessage = "Failed to load page data.";
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await BackupDatabase();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to save backup on dispose");
            }
        }

        private async Task OnCategoryChanged(ChangeEventArgs e)
        {
            var categoryId = e.Value?.ToString();
            if (string.IsNullOrEmpty(categoryId))
            {
                selectedCategory = null;
                relations.Clear();
                suggestedPair = null;
            }
            else
            {
                selectedCategory = categories.FirstOrDefault(c => c.Id == categoryId);
                await RefreshRelations();
            }
        }

        private async Task RefreshRelations()
        {
            if (selectedCategory != null)
            {
                relations = (await RelationRepository.GetByCategoryIdAsync(selectedCategory.Id)).ToList();
                FindSuggestedPair();
            }
        }

        private async Task BackupDatabase()
        {
            var data = await ContestManager.ExportDataAsync();
            if (data.Length > 0)
            {
                await BackupService.SaveBackupAsync(data);
            }
        }

        private void GeneratePartitions()
        {
            if (entries.Count < 2) return;
            currentPartitions = PartitionService.GeneratePartitions(entries.Select(e => e.Id), kPartitions, overlapRate);
            selectedPartitionId = currentPartitions.Keys.First();
            FindSuggestedPair();
        }

        private void OnPartitionChanged(ChangeEventArgs e)
        {
            selectedPartitionId = e.Value?.ToString();
            FindSuggestedPair();
        }

        private IEnumerable<Entry> GetFilteredEntries()
        {
            if (currentPartitions != null && !string.IsNullOrEmpty(selectedPartitionId))
            {
                var partitionEntries = currentPartitions[selectedPartitionId];
                return entries.Where(e => partitionEntries.Contains(e.Id));
            }
            return entries;
        }

        private void FindSuggestedPair()
        {
            suggestedPair = null;
            var filteredEntries = GetFilteredEntries().ToList();
            if (filteredEntries.Count < 2) return;

            var existingPairs = new HashSet<(string, string)>();
            foreach (var rel in relations)
            {
                existingPairs.Add((rel.EntryA.Id, rel.EntryB.Id));
                existingPairs.Add((rel.EntryB.Id, rel.EntryA.Id));
            }

            for (int i = 0; i < filteredEntries.Count; i++)
            {
                for (int j = i + 1; j < filteredEntries.Count; j++)
                {
                    var a = filteredEntries[i].Id;
                    var b = filteredEntries[j].Id;
                    if (!existingPairs.Contains((a, b)))
                    {
                        suggestedPair = (a, b);
                        return;
                    }
                }
            }
        }

        private void ApplySuggestion()
        {
            if (suggestedPair != null)
            {
                entryAId = suggestedPair.Value.A;
                entryBId = suggestedPair.Value.B;
            }
        }

        private async Task RecordResult(Operator resultOp)
        {
            if (suggestedPair == null || selectedCategory == null) return;

            var entryA = entries.First(e => e.Id == suggestedPair.Value.A);
            var entryB = entries.First(e => e.Id == suggestedPair.Value.B);
            var relation = new Relation(selectedCategory, entryA, resultOp, entryB);

            await RelationRepository.AddAsync(relation);
            await BackupDatabase();
            await RefreshRelations();
        }

        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (suggestedPair == null) return;

            switch (e.Key.ToLower())
            {
                case "a":
                case "arrowleft":
                    await RecordResult(Operator.GreaterThan);
                    break;
                case "s":
                case "arrowdown":
                    await RecordResult(Operator.EqualTo);
                    break;
                case "d":
                case "arrowright":
                    await RecordResult(Operator.LessThan);
                    break;
            }
        }

        private async Task HandleCardKeyDown(KeyboardEventArgs e, Operator resultOp)
        {
            if (e.Key == "Enter" || e.Key == " ")
            {
                await RecordResult(resultOp);
            }
        }

        private async Task AddRelation()
        {
            errorMessage = "";
            if (selectedCategory == null) return;
            if (string.IsNullOrEmpty(entryAId) || string.IsNullOrEmpty(entryBId))
            {
                errorMessage = "Please select both entries.";
                return;
            }
            if (entryAId == entryBId)
            {
                errorMessage = "Entry A and Entry B must be different.";
                return;
            }

            var entryA = entries.First(e => e.Id == entryAId);
            var entryB = entries.First(e => e.Id == entryBId);
            var relation = new Relation(selectedCategory, entryA, op, entryB);

            await RelationRepository.AddAsync(relation);
            await BackupDatabase();
            await RefreshRelations();
        }

        private async Task DeleteRelation(Relation rel)
        {
            if (selectedCategory == null) return;
            await RelationRepository.DeleteAsync(selectedCategory.Id, rel.EntryA.Id, rel.EntryB.Id);
            await BackupDatabase();
            await RefreshRelations();
        }

        private string GetOpText(Operator o) => o switch
        {
            Operator.GreaterThan => "is better than ( > )",
            Operator.LessThan => "is worse than ( < )",
            Operator.EqualTo => "is equal to ( = )",
            _ => "unknown"
        };
    }
}
