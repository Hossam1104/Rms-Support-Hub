using Dapper;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Data.Repositories;

public class FlatOrderItemRepository : IItemRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public FlatOrderItemRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Product?> LookupItemAsync(string connectionString, string materialNumber, string? branchCode = null)
    {
        if (string.IsNullOrWhiteSpace(materialNumber) || !materialNumber.All(char.IsDigit) || materialNumber.Length != 6)
        {
            throw new ArgumentException("Material number must be exactly 6 digits.", nameof(materialNumber));
        }

        const string sql = @"
            SELECT TOP 1
                I.MaterialNumber AS ItemCode,
                I.Name AS ItemName,
                IP.Price AS UnitPrice,
                TT.Rate AS VatPercentage
            FROM dbo.Items AS I
            LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
            INNER JOIN dbo.ItemUnitOfMeasures AS IUM ON I.Id = IUM.ItemId
            LEFT JOIN dbo.ItemPrices AS IP ON IUM.Id = IP.ItemUnitOfMeasureId
            WHERE RIGHT(I.MaterialNumber, 6) = @MaterialNumber
              AND IP.IsActive = 1
              AND IP.Price IS NOT NULL
              AND IP.ToDate > GETDATE()
            ORDER BY I.Id DESC";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<ItemLookupQueryResult>(sql, new { MaterialNumber = materialNumber });

        if (row == null) return null;

        return new Product
        {
            ItemCode = row.ItemCode ?? $"000000000000{materialNumber}",
            ItemName = row.ItemName ?? $"Item {materialNumber}",
            UnitPrice = row.UnitPrice ?? 0m,
            VatPercentage = row.VatPercentage ?? 0m,
            Quantity = 1m,
            Discount = 0m
        };
    }

    private class ItemLookupQueryResult
    {
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? VatPercentage { get; set; }
    }
}
