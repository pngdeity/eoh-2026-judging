using System.Collections.Generic;
using System.Threading.Tasks;

using ContestJudging.Core.Entities;

namespace ContestJudging.Core.Interfaces.Repositories
{
    public interface ICategoryRepository
    {
        Task<Category?> GetByIdAsync(string id);
        Task<IEnumerable<Category>> GetAllAsync();
        Task AddAsync(Category category);
        Task UpdateAsync(Category category);
        Task DeleteAsync(string id);
    }
}
