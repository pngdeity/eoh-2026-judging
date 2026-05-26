using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;

namespace ContestJudging.Core.Interfaces.Repositories
{
    public interface IEntryRepository
    {
        Task<Entry?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Entry>> GetAllAsync(CancellationToken cancellationToken = default);
        Task AddAsync(Entry entry, CancellationToken cancellationToken = default);
        Task UpdateAsync(Entry entry, CancellationToken cancellationToken = default);
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    }
}
