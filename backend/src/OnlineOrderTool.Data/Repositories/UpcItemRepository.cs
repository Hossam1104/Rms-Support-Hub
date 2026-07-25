using Dapper;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Data.Repositories;

public class UpcItemRepository : IItemRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public UpcItemRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Product?> LookupItemAsync(string connectionString, string materialNumber, string? branchCode = null)
    {
        var cleanCode = (materialNumber ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cleanCode))
        {
            throw new ArgumentException("Item search term cannot be empty.", nameof(materialNumber));
        }

        var cleanBranch = (branchCode ?? "").Trim();

        const string sql = @"
            SELECT TOP 1
                I.MaterialNumber AS ItemCode,
                I.Name AS ItemName,
                IP.Price AS UnitPrice,
                TT.Rate AS VatPercentage,
                CASE WHEN B.Code = @BranchCode THEN 1 ELSE 0 END AS BranchMatchRank
            FROM dbo.Items AS I
            LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
            INNER JOIN dbo.ItemUnitOfMeasures AS IUM ON I.Id = IUM.ItemId
            LEFT JOIN dbo.ItemPrices AS IP ON IUM.Id = IP.ItemUnitOfMeasureId
            LEFT JOIN dbo.Branches AS B ON IP.BranchId = B.Id
            WHERE (RIGHT(I.MaterialNumber, 6) = @Code OR I.MaterialNumber = @Code)
              AND IP.IsActive = 1
              AND IP.Price IS NOT NULL
              AND IP.ToDate > GETDATE()
            ORDER BY BranchMatchRank DESC, IP.Id DESC";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<UpcItemQueryResult>(sql, new { Code = cleanCode, BranchCode = cleanBranch });

        if (row == null) return null;

        return new Product
        {
            ItemCode = row.ItemCode ?? cleanCode,
            ItemName = row.ItemName ?? $"Item {cleanCode}",
            UnitPrice = row.UnitPrice ?? 0m,
            VatPercentage = row.VatPercentage ?? 0m,
            Quantity = 1m,
            Discount = 0m
        };
    }

    private class UpcItemQueryResult
    {
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? VatPercentage { get; set; }
    }
}
