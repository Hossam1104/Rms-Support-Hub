using Dapper;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Data.Repositories;

public class UpcConsumerRepository : IConsumerRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public UpcConsumerRepository(ISqlServerConnectionFactory connectionFactory)
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

        using var connection = _connectionFactory.CreateConnection(connectionString);

        // Try standard dbo.Customers table first
        try
        {
            const string sqlCustomers = @"
                SELECT TOP 1
                    ISNULL(C.FirstName, '') + ' ' + ISNULL(C.LastName, '') AS FirstName,
                    C.CustomerNumber AS ConsumerCode,
                    C.PhoneNumber AS PrimaryPhoneNumber,
                    C.Email,
                    C.NationalId AS AddressLine,
                    C.Nationality AS DistrictName
                FROM dbo.Customers AS C
                WHERE C.PhoneNumber IN (@pExact, @pWithZero, @pWithoutZero, @pWith966, @pWithPlus966)
                ORDER BY C.Id DESC";

            var rowCust = await connection.QueryFirstOrDefaultAsync<UpcConsumerQueryResult>(sqlCustomers, new
            {
                pExact, pWithZero, pWithoutZero, pWith966, pWithPlus966
            });

            if (rowCust != null)
            {
                return new Consumer
                {
                    FirstName = string.IsNullOrWhiteSpace(rowCust.FirstName) ? "Customer" : rowCust.FirstName.Trim(),
                    ConsumerCode = rowCust.ConsumerCode ?? "",
                    PrimaryPhoneNumber = rowCust.PrimaryPhoneNumber ?? rawPhone,
                    Email = rowCust.Email ?? "",
                    NationalId = rowCust.AddressLine ?? "",
                    Nationality = rowCust.DistrictName ?? ""
                };
            }
        }
        catch
        {
            // Ignore dbo.Customers error and try dbo.Consumers table
        }

        // Fallback to dbo.Consumers table
        try
        {
            const string sqlConsumers = @"
                SELECT TOP 1
                    C.Name AS FirstName,
                    C.Code AS ConsumerCode,
                    C.MobileNumber AS PrimaryPhoneNumber,
                    C.Email
                FROM dbo.Consumers AS C
                WHERE C.MobileNumber IN (@pExact, @pWithZero, @pWithoutZero, @pWith966, @pWithPlus966)
                ORDER BY C.Id DESC";

            var rowCons = await connection.QueryFirstOrDefaultAsync<UpcConsumerQueryResult>(sqlConsumers, new
            {
                pExact, pWithZero, pWithoutZero, pWith966, pWithPlus966
            });

            if (rowCons != null)
            {
                return new Consumer
                {
                    FirstName = rowCons.FirstName ?? "Consumer",
                    ConsumerCode = rowCons.ConsumerCode ?? "",
                    PrimaryPhoneNumber = rowCons.PrimaryPhoneNumber ?? rawPhone,
                    Email = rowCons.Email ?? ""
                };
            }
        }
        catch
        {
            // Ignore dbo.Consumers error
        }

        return null;
    }

    private class UpcConsumerQueryResult
    {
        public string? FirstName { get; set; }
        public string? ConsumerCode { get; set; }
        public string? PrimaryPhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? AddressLine { get; set; }
        public string? DistrictName { get; set; }
        public string? CityName { get; set; }
    }
}
