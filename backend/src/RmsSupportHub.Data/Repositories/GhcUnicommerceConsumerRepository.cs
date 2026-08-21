using Dapper;
using RmsSupportHub.Core;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Repositories;

namespace RmsSupportHub.Data.Repositories;

/// <summary>
/// Read-only Uni-Commerce consumer lookup for the verified
/// RmsEcommerceStg.Consumers schema. Uni-Commerce stores the phone in
/// PrimaryPhoneNumber and does not expose the flat-order loyalty-address
/// table, so it must not reuse the GHC E-Commerce address query.
/// </summary>
public sealed class GhcUnicommerceConsumerRepository : IGhcUnicommerceConsumerRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public GhcUnicommerceConsumerRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone)
    {
        var canonicalPhone = Normalizers.NormalizePhoneSearch(phone);

        const string sql = @"
            SELECT TOP 1
                C.Id, C.FirstName, C.MiddleName, C.LastName,
                C.Email, C.PrimaryPhoneNumber, C.Gender, C.BirthDate, C.ConsumerCode
            FROM dbo.Consumers AS C
            WHERE RIGHT(C.PrimaryPhoneNumber, 9) = @Phone9
            ORDER BY C.Id DESC";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<UniConsumerQueryResult>(
            sql,
            new { Phone9 = canonicalPhone });

        if (row == null) return null;

        return new Consumer
        {
            FirstName = row.FirstName ?? "",
            MiddleName = row.MiddleName ?? "",
            LastName = row.LastName ?? "",
            ConsumerCode = row.ConsumerCode ?? "",
            Gender = row.Gender ?? "",
            BirthDate = row.BirthDate?.ToString("yyyy-MM-dd"),
            PrimaryPhoneNumber = row.PrimaryPhoneNumber ?? phone,
            Email = row.Email ?? "",
            Address = "",
            AddressCode = ""
        };
    }

    private sealed class UniConsumerQueryResult
    {
        public long Id { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PrimaryPhoneNumber { get; set; }
        public string? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? ConsumerCode { get; set; }
    }
}
