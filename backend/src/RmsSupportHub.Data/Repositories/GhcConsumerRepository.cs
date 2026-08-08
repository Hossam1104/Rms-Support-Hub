using Dapper;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Repositories;

namespace RmsSupportHub.Data.Repositories;

public class GhcConsumerRepository : IGhcConsumerRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public GhcConsumerRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // TODO(db-creds): confirm the real table/column names once real GHC
    // database credentials/schema are supplied. This guesses a dbo.Customers
    // shape consistent with the customer_number EXISTS-filter already used in
    // FlatOrderItemRepository (ported from the retired Flask
    // lookup_consumer_by_phone, which carried the same TODO; see Git history).
    // Unlike UPC's Consumers/LoyaltyConsumerAddresses lookup, this has never
    // been verified live.
    public async Task<Consumer?> LookupConsumerByPhoneAsync(string connectionString, string phone)
    {
        var rawPhone = (phone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            throw new ArgumentException("Phone number is required.", nameof(phone));
        }

        var digitsOnly = new string(rawPhone.Where(char.IsDigit).ToArray());
        var core9Digits = digitsOnly.Length >= 9 ? digitsOnly[^9..] : digitsOnly;

        var pExact = rawPhone;
        var pWithZero = $"0{core9Digits}";
        var pWithoutZero = core9Digits;
        var pWith966 = $"966{core9Digits}";
        var pWithPlus966 = $"+966{core9Digits}";

        const string sql = @"
            SELECT TOP 1
                C.FirstName, C.MiddleName, C.LastName, C.CustomerNumber AS ConsumerCode,
                C.Gender, C.BirthDate, C.PhoneNumber AS PrimaryPhoneNumber,
                C.Email, C.NationalId, C.Nationality
            FROM dbo.Customers AS C
            WHERE C.PhoneNumber IN (@pExact, @pWithZero, @pWithoutZero, @pWith966, @pWithPlus966)
              AND C.IsActive = 1
            ORDER BY C.Id DESC";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<ConsumerQueryResult>(sql, new
        {
            pExact,
            pWithZero,
            pWithoutZero,
            pWith966,
            pWithPlus966
        });

        if (row == null) return null;

        return new Consumer
        {
            FirstName = row.FirstName ?? "",
            MiddleName = row.MiddleName ?? "",
            LastName = row.LastName ?? "",
            ConsumerCode = row.ConsumerCode ?? "",
            Gender = row.Gender ?? "",
            BirthDate = row.BirthDate?.ToString("yyyy-MM-dd"),
            PrimaryPhoneNumber = row.PrimaryPhoneNumber ?? rawPhone,
            Email = row.Email ?? "",
            NationalId = row.NationalId ?? "",
            Nationality = row.Nationality ?? ""
        };
    }

    private class ConsumerQueryResult
    {
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? ConsumerCode { get; set; }
        public string? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? PrimaryPhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? NationalId { get; set; }
        public string? Nationality { get; set; }
    }
}
