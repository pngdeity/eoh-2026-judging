using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;

namespace ContestJudging.Core.Interfaces.Repositories
{
    public interface IRelationRepository
    {
        Task<IEnumerable<Relation>> GetByCategoryIdAsync(string categoryId, CancellationToken cancellationToken = default);
        Task AddAsync(Relation relation, CancellationToken cancellationToken = default);
        Task DeleteAsync(string categoryId, string entryAId, string entryBId, CancellationToken cancellationToken = default);
    }
}
