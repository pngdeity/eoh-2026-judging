using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Core.Interfaces.Repositories;
using ContestJudging.Services.Validation;

namespace ContestJudging.Services.Managers
{
    public class ContestManager : IContestManager
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IEntryRepository _entryRepository;
        private readonly IRelationRepository _relationRepository;
        private readonly IValidationService _validationService;

        public ContestManager(
            ICategoryRepository categoryRepository,
            IEntryRepository entryRepository,
            IRelationRepository relationRepository,
            IValidationService validationService)
        {
            _categoryRepository = categoryRepository;
            _entryRepository = entryRepository;
            _relationRepository = relationRepository;
            _validationService = validationService;
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
    }
}
