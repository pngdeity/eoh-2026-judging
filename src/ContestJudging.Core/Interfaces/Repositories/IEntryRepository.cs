using System.Collections.Generic;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;

namespace ContestJudging.Core.Interfaces.Repositories
{
    public interface IEntryRepository
    {
        Task<Entry?> GetByIdAsync(string id);
        Task<IEnumerable<Entry>> GetAllAsync();
        Task AddAsync(Entry entry);
        Task UpdateAsync(Entry entry);
        Task DeleteAsync(string id);
    }
}
