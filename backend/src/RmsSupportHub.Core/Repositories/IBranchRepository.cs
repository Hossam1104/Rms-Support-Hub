using RmsSupportHub.Core.DTOs;

namespace RmsSupportHub.Core.Repositories;

public interface IBranchRepository
{
    Task<List<BranchOptionDto>> ListBranchesAsync(string connectionString);
}
