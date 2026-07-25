using Dapper;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Data.Repositories;

public class GhcConsumerRepository : IConsumerRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public GhcConsumerRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

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
            Email = rawRowEmail(row),
            NationalId = row.NationalId ?? "",
            Nationality = row.Nationality ?? ""
        };
    }

    private static string rawRowEmail(ConsumerQueryResult row) => row.Email ?? "";

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
