using System.Collections.Generic;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;

namespace ContestJudging.Services.Managers
{
    public interface IContestManager
    {
        Task AddCategoryAsync(Category category);
        Task AddEntryAsync(Entry entry);
        Task AddRelationAsync(Relation relation);
        Task<bool> ValidateCategoryRelationsAsync(string categoryId);
        Task<bool> CheckTotalOrderAsync(string categoryId);
    }
}
