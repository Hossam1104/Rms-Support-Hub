using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Data.Repositories;

public interface IItemRepository
{
    Task<Product?> LookupItemAsync(string connectionString, string materialNumber, string? branchCode = null);
}
