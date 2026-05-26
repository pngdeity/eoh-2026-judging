using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;
using ContestJudging.Services.Validation;

namespace ContestJudging.Services.Managers
{
    public interface IContestManager
    {
        Task AddCategoryAsync(Category category, CancellationToken cancellationToken = default);
        Task AddEntryAsync(Entry entry, CancellationToken cancellationToken = default);
        Task AddEntriesAsync(IEnumerable<Entry> entries, CancellationToken cancellationToken = default);
        Task DeleteCategoryAsync(string categoryId, CancellationToken cancellationToken = default);
        Task DeleteEntryAsync(string entryId, CancellationToken cancellationToken = default);
        Task AddRelationAsync(Relation relation, CancellationToken cancellationToken = default);
        Task DeleteRelationAsync(string categoryId, string entryAId, string entryBId, CancellationToken cancellationToken = default);
        Task<bool> ValidateCategoryRelationsAsync(string categoryId, CancellationToken cancellationToken = default);
        Task<bool> CheckTotalOrderAsync(string categoryId, CancellationToken cancellationToken = default);

        // NEW: Orchestrates the Partitioned Judging pipeline
        Task<ValidationResult> CalculateGlobalScoresAsync(string categoryId, double maxScore, CancellationToken cancellationToken = default);

        // TRICKY OPTIMIZATION #2
        Task<byte[]> ExportDataAsync(CancellationToken cancellationToken = default);
        Task ImportDataAsync(byte[] data, CancellationToken cancellationToken = default);
    }
}
