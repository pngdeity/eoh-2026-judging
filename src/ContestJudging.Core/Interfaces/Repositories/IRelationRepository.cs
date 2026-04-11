using System.Collections.Generic;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;

namespace ContestJudging.Core.Interfaces.Repositories
{
    public interface IRelationRepository
    {
        Task<IEnumerable<Relation>> GetByCategoryIdAsync(string categoryId);
        Task AddAsync(Relation relation);
        Task DeleteAsync(string categoryId, string entryAId, string entryBId);
    }
}
