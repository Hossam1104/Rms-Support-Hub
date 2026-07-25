using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Data.Repositories;

public interface IConsumerRepository
{
    Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone);
}
