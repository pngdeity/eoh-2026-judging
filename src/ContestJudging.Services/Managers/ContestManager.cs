using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Services.Resolution;
using ContestJudging.Services.Validation;

namespace ContestJudging.Services.Managers
{
    public sealed class ContestManager : IContestManager
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IEntryRepository _entryRepository;
        private readonly IRelationRepository _relationRepository;
        private readonly IValidationService _validationService;
        private readonly IGlobalRankingService _globalRankingService;
        private readonly IScoringStrategy _scoringStrategy;
        private readonly IDatabaseBackupService _backupService;

        public ContestManager(
            ICategoryRepository categoryRepository,
            IEntryRepository entryRepository,
            IRelationRepository relationRepository,
            IValidationService validationService,
            IGlobalRankingService globalRankingService,
            IScoringStrategy scoringStrategy,
            IDatabaseBackupService backupService)
        {
            _categoryRepository = categoryRepository;
            _entryRepository = entryRepository;
            _relationRepository = relationRepository;
            _validationService = validationService;
            _globalRankingService = globalRankingService;
            _scoringStrategy = scoringStrategy;
            _backupService = backupService;
        }

        public async Task AddCategoryAsync(Category category, CancellationToken cancellationToken = default)
        {
            await _categoryRepository.AddAsync(category, cancellationToken);
        }

        public async Task AddEntryAsync(Entry entry, CancellationToken cancellationToken = default)
        {
            await _entryRepository.AddAsync(entry, cancellationToken);
        }

        public async Task AddEntriesAsync(IEnumerable<Entry> entries, CancellationToken cancellationToken = default)
        {
            foreach (var entry in entries)
            {
                await _entryRepository.AddAsync(entry, cancellationToken);
            }
        }

        public async Task DeleteCategoryAsync(string categoryId, CancellationToken cancellationToken = default)
        {
            await _categoryRepository.DeleteAsync(categoryId, cancellationToken);
        }

        public async Task DeleteEntryAsync(string entryId, CancellationToken cancellationToken = default)
        {
            await _entryRepository.DeleteAsync(entryId, cancellationToken);
        }

        public async Task AddRelationAsync(Relation relation, CancellationToken cancellationToken = default)
        {
            await _relationRepository.AddAsync(relation, cancellationToken);
        }

        public async Task DeleteRelationAsync(string categoryId, string entryAId, string entryBId, CancellationToken cancellationToken = default)
        {
            await _relationRepository.DeleteAsync(categoryId, entryAId, entryBId, cancellationToken);
        }

        public async Task<bool> ValidateCategoryRelationsAsync(string categoryId, CancellationToken cancellationToken = default)
        {
            var relations = (await _relationRepository.GetByCategoryIdAsync(categoryId, cancellationToken)).ToList();
            if (!relations.Any()) return false;

            var entries = (await _entryRepository.GetAllAsync(cancellationToken)).ToList();
            var entriesInRelations = new HashSet<string>();
            foreach (var relation in relations)
            {
                entriesInRelations.Add(relation.EntryA.Id);
                entriesInRelations.Add(relation.EntryB.Id);
            }

            return entries.All(e => entriesInRelations.Contains(e.Id));
        }

        public async Task<bool> CheckTotalOrderAsync(string categoryId, CancellationToken cancellationToken = default)
        {
            var relations = await _relationRepository.GetByCategoryIdAsync(categoryId, cancellationToken);
            var entries = await _entryRepository.GetAllAsync(cancellationToken);
            return _validationService.IsTotalOrder(relations, entries.Select(e => e.Id));
        }

        public async Task<ValidationResult> CalculateGlobalScoresAsync(string categoryId, double maxScore, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
            if (category == null)
            {
                return new ValidationResult(false, "Category not found.", 0);
            }

            var relations = (await _relationRepository.GetByCategoryIdAsync(categoryId, cancellationToken)).ToList();
            var entries = (await _entryRepository.GetAllAsync(cancellationToken)).ToList();
            var allEntryIds = entries.Select(e => e.Id).ToList();

            var validationResult = _validationService.ValidatePartitionedGraph(relations, allEntryIds);
            if (!validationResult.IsValid)
            {
                return validationResult;
            }

            var strengths = _globalRankingService.ResolveGlobalStrengths(relations, allEntryIds);

            var scores = _scoringStrategy.CalculateScoresFromStrengths(strengths, maxScore);

            foreach (var entry in entries)
            {
                if (scores.TryGetValue(entry.Id, out double score))
                {
                    entry.SetScore(category, score);
                    await _entryRepository.UpdateAsync(entry, cancellationToken);
                }
            }

            return validationResult;
        }

        public async Task<byte[]> ExportDataAsync(CancellationToken cancellationToken = default)
        {
            return await _backupService.ExportAsync(cancellationToken);
        }

        public async Task ImportDataAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            await _backupService.ImportAsync(data, cancellationToken);
        }
    }
}
