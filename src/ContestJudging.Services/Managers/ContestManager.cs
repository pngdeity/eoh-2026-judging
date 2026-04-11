using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Services.Resolution;
using ContestJudging.Services.Validation;

namespace ContestJudging.Services.Managers
{
    public class ContestManager : IContestManager
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IEntryRepository _entryRepository;
        private readonly IRelationRepository _relationRepository;
        private readonly IValidationService _validationService;
        private readonly IGlobalRankingService _globalRankingService;
        private readonly IScoringStrategy _scoringStrategy;

        public ContestManager(
            ICategoryRepository categoryRepository,
            IEntryRepository entryRepository,
            IRelationRepository relationRepository,
            IValidationService validationService,
            IGlobalRankingService globalRankingService,
            IScoringStrategy scoringStrategy)
        {
            _categoryRepository = categoryRepository;
            _entryRepository = entryRepository;
            _relationRepository = relationRepository;
            _validationService = validationService;
            _globalRankingService = globalRankingService;
            _scoringStrategy = scoringStrategy;
        }

        public async Task AddCategoryAsync(Category category)
        {
            await _categoryRepository.AddAsync(category);
        }

        public async Task AddEntryAsync(Entry entry)
        {
            await _entryRepository.AddAsync(entry);
        }

        public async Task AddRelationAsync(Relation relation)
        {
            await _relationRepository.AddAsync(relation);
        }

        public async Task<bool> ValidateCategoryRelationsAsync(string categoryId)
        {
            var relations = (await _relationRepository.GetByCategoryIdAsync(categoryId)).ToList();
            if (!relations.Any()) return false;

            var entries = (await _entryRepository.GetAllAsync()).ToList();
            var entriesInRelations = new HashSet<string>();
            foreach (var relation in relations)
            {
                entriesInRelations.Add(relation.EntryA.Id);
                entriesInRelations.Add(relation.EntryB.Id);
            }

            return entries.All(e => entriesInRelations.Contains(e.Id));
        }

        public async Task<bool> CheckTotalOrderAsync(string categoryId)
        {
            var relations = await _relationRepository.GetByCategoryIdAsync(categoryId);
            var entries = await _entryRepository.GetAllAsync();
            return _validationService.IsTotalOrder(relations, entries.Select(e => e.Id));
        }

        public async Task<ValidationResult> CalculateGlobalScoresAsync(string categoryId, double maxScore)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category == null)
            {
                return new ValidationResult(false, "Category not found.", 0);
            }

            var relations = (await _relationRepository.GetByCategoryIdAsync(categoryId)).ToList();
            var entries = (await _entryRepository.GetAllAsync()).ToList();
            var allEntryIds = entries.Select(e => e.Id).ToList();

            // 1. Validation
            var validationResult = _validationService.ValidatePartitionedGraph(relations, allEntryIds);
            if (!validationResult.IsValid)
            {
                return validationResult;
            }

            // 2. Statistical Resolution
            var strengths = _globalRankingService.ResolveGlobalStrengths(relations, allEntryIds);

            // 3. Score Translation
            var scores = _scoringStrategy.CalculateScoresFromStrengths(strengths, maxScore);

            // 4. Update Entries
            foreach (var entry in entries)
            {
                if (scores.TryGetValue(entry.Id, out double score))
                {
                    entry.SetScore(category, score);
                    await _entryRepository.UpdateAsync(entry);
                }
            }

            return validationResult;
        }
    }
}
