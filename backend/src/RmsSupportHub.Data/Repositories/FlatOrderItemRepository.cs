using Dapper;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Repositories;

namespace RmsSupportHub.Data.Repositories;

public class FlatOrderItemRepository : IGhcItemRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public FlatOrderItemRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// GHC item lookup by six-digit material suffix. The query uses the live
    /// GHC Testing schema: Items, BranchItemUnitOfMeasures,
    /// ItemUnitOfMeasureBarCodes, ItemUnitOfMeasurePrices, Branches, and
    /// TaxTypes. See docs/database-schema.md for the verified mapping.
    /// </summary>
    public async Task<Product?> LookupItemAsync(string connectionString, string materialNumber, string? branchCode = null)
    {
        if (string.IsNullOrWhiteSpace(materialNumber) || !materialNumber.All(char.IsDigit) || materialNumber.Length != 6)
        {
            throw new ArgumentException("Material number must be exactly 6 digits.", nameof(materialNumber));
        }

        const string sql = @"
            SELECT TOP 1
                I.MaterialNumber AS ItemCode,
                IUOMB.UniversalBarCode AS ItemBarcode,
                I.Name AS ItemNameEn,
                I.NativeName AS ItemNameAr,
                IP.Price AS UnitPrice,
                TT.Rate AS VatPercentage
            FROM dbo.Items AS I
                LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
                INNER JOIN dbo.BranchItemUnitOfMeasures AS BIUOM ON I.Id = BIUOM.ItemId
                INNER JOIN dbo.ItemUnitOfMeasureBarCodes AS IUOMB ON BIUOM.Id = IUOMB.BranchItemUnitOfMeasureId
                LEFT JOIN dbo.ItemUnitOfMeasurePrices AS IP ON BIUOM.Id = IP.BranchItemUnitOfMeasureId
                LEFT JOIN dbo.Branches AS B ON B.Id = BIUOM.BranchId
            WHERE RIGHT(I.MaterialNumber, 6) = @MaterialNumber
              AND I.IsDeleted = 0
              AND (@BranchCode IS NULL OR B.BranchCode = @BranchCode)
              AND IP.IsActive = 1
              AND IP.Price IS NOT NULL
              AND (IP.ToDate IS NULL OR IP.ToDate > GETDATE())
            ORDER BY I.Id DESC, BIUOM.IsBase DESC, IP.Price DESC";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<GhcItemQueryResult>(
            sql,
            new
            {
                MaterialNumber = materialNumber,
                BranchCode = string.IsNullOrWhiteSpace(branchCode) ? null : branchCode.Trim()
            });

        if (row == null) return null;

        return new Product
        {
            ItemCode = row.ItemCode ?? $"000000000000{materialNumber}",
            ItemBarcode = row.ItemBarcode,
            ItemName = row.ItemNameEn ?? $"Item {materialNumber}",
            ItemNameAr = row.ItemNameAr,
            UnitPrice = row.UnitPrice ?? 0m,
            VatPercentage = row.VatPercentage ?? 0m,
            Quantity = 1m,
            Discount = 0m
        };
    }

    private sealed class GhcItemQueryResult
    {
        public string? ItemCode { get; set; }
        public string? ItemBarcode { get; set; }
        public string? ItemNameEn { get; set; }
        public string? ItemNameAr { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? VatPercentage { get; set; }
    }
}
