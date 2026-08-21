using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Repositories;

namespace RmsSupportHub.Data.Repositories;

/// <summary>
/// GHC and GHC Uni-Commerce use the same consumer master-data schema as the
/// verified flat-order flow. Keep one SQL implementation for the shared
/// Consumers/LoyaltyConsumerAddresses lookup so the two GHC lanes cannot drift.
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
