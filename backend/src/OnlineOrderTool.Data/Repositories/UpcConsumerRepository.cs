using Dapper;
using OnlineOrderTool.Core;
using OnlineOrderTool.Core.Models;
using OnlineOrderTool.Core.Repositories;

namespace OnlineOrderTool.Data.Repositories;

public class UpcConsumerRepository : IUpcConsumerRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public UpcConsumerRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>UPC consumer + loyalty address lookup by phone. Verified live
    /// against Consumers / LoyaltyConsumerAddresses on RmsMainTest2 -- see
    /// docs/database-schema.md §3.3. LoyaltyConsumerAddresses has no single
    /// "the" address per consumer, so the OUTER APPLY prefers the row flagged
    /// IsMaster, falling back to the most recently added address if no master
    /// is set. Replaces the previous speculative dbo.Customers/dbo.Consumers
    /// dual-fallback query, which referenced columns that do not exist on
    /// this schema (Consumers has no Code/MobileNumber columns).</summary>
    public async Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone)
    {
        var canonicalPhone = Normalizers.NormalizePhoneSearch(phone);

        const string sql = @"
            SELECT TOP 1
                C.Id, C.FirstName, C.MiddleName, C.LastName,
                C.Email, C.PhoneNumber, C.Gender, C.BirthDate, C.ConsumerCode,
                A.FullAddress, A.AddressCode, A.StreetName, A.Building,
                A.Floor, A.Landmark, A.Area
            FROM Consumers AS C
                OUTER APPLY (
                    SELECT TOP 1
                        FullAddress, AddressCode, StreetName, Building, Floor, Landmark, Area
                    FROM LoyaltyConsumerAddresses
                    WHERE ConsumerId = C.Id
                    ORDER BY IsMaster DESC, Id DESC
                ) AS A
            WHERE RIGHT(C.PhoneNumber, 9) = @Phone9
            ORDER BY C.Id DESC";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<UpcConsumerQueryResult>(sql, new { Phone9 = canonicalPhone });

        if (row == null) return null;

        var fullAddress = row.FullAddress;
        if (string.IsNullOrWhiteSpace(fullAddress))
        {
            // Compose a best-effort address from the parts when FullAddress
            // itself is blank, matching lookup_upc_consumer_by_phone.
            var parts = new[] { row.StreetName, row.Building, row.Floor, row.Landmark, row.Area }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            fullAddress = string.Join(", ", parts);
        }

        return new Consumer
        {
            FirstName = row.FirstName ?? "",
            MiddleName = row.MiddleName ?? "",
            LastName = row.LastName ?? "",
            ConsumerCode = row.ConsumerCode ?? "",
            Gender = row.Gender ?? "",
            BirthDate = row.BirthDate?.ToString("yyyy-MM-dd"),
            PrimaryPhoneNumber = row.PhoneNumber ?? phone,
            Email = row.Email ?? "",
            Address = fullAddress,
            AddressCode = row.AddressCode ?? ""
        };
    }

    private class UpcConsumerQueryResult
    {
        public long Id { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? ConsumerCode { get; set; }
        public string? FullAddress { get; set; }
        public string? AddressCode { get; set; }
        public string? StreetName { get; set; }
        public string? Building { get; set; }
        public string? Floor { get; set; }
        public string? Landmark { get; set; }
        public string? Area { get; set; }
    }
}
