using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Core.Repositories;

public interface IConsumerRepository
{
    Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone);
}

/// <summary>See IGhcItemRepository/IUpcItemRepository in IItemRepository.cs
/// for why these marker interfaces exist.</summary>
public interface IGhcConsumerRepository : IConsumerRepository { }
public interface IUpcConsumerRepository : IConsumerRepository { }
