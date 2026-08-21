using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Repositories;

namespace RmsSupportHub.Data.Repositories;

/// <summary>
/// GHC E-Commerce reuses the verified compatible shared consumer logic with the
/// flat-order flow (Consumers/LoyaltyConsumerAddresses lookup). GHC Uni-Commerce
/// uses its own dedicated, verified dbo.Consumers repository (GhcUnicommerceConsumerRepository).
/// </summary>
public sealed class GhcConsumerRepository : IGhcConsumerRepository
{
    private readonly UpcConsumerRepository _sharedConsumerRepository;

    public GhcConsumerRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _sharedConsumerRepository = new UpcConsumerRepository(connectionFactory);
    }

    public Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone) =>
        _sharedConsumerRepository.LookupConsumerByPhoneAsync(connectionString, phone);
}
