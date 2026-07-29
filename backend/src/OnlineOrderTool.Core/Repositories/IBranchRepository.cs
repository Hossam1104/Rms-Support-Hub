using OnlineOrderTool.Core.DTOs;

namespace OnlineOrderTool.Core.Repositories;

public interface IBranchRepository
{
    Task<List<BranchOptionDto>> ListBranchesAsync(string connectionString);
}
