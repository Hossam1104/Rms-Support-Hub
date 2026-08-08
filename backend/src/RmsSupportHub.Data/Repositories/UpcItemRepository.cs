using Dapper;
using RmsSupportHub.Core;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Repositories;

namespace RmsSupportHub.Data.Repositories;

public class UpcItemRepository : IUpcItemRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public UpcItemRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>Branch-specific item pricing lookup. Verified live against
    /// Items / BranchItemUnitOfMeasures / ItemUnitOfMeasurePrices / Branches
    /// on RmsMainTest2 -- see docs/database-schema.md §3.2. Unlike GHC's
    /// still-unverified lookup, price here is branch-specific
    /// (BranchItemUnitOfMeasures joins Items to Branches), so branchCode is
    /// required, not optional. When an item has more than one unit-of-
    /// measure/price row for the same branch, the base unit of measure wins,
    /// then the highest price (mirrors the query's own ROW_NUMBER ordering).
    /// Has no barcode source -- matches legacy, which always returns "" for
    /// UPC's item_Barcode.</summary>
    public async Task<Product?> LookupItemAsync(string connectionString, string materialNumber, string? branchCode = null)
    {
        var padded = Normalizers.NormalizeUpcMaterialNumber(materialNumber);

        var cleanBranch = (branchCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cleanBranch))
        {
            throw new ArgumentException("Branch code is required to look up UPC item pricing.", nameof(branchCode));
        }

        const string sql = @"
            WITH ItemPricesRanked AS (
                SELECT
                    B.BranchCode AS Branch_Code,
                    I.MaterialNumber AS Full_Material_Number,
                    I.Name AS Item_Name_EN,
                    I.NativeName AS Item_Name_AR,
                    IUOMP.Price AS Item_Price,
                    BIUOM.IsBase AS IsBase,
                    TT.Rate AS Tax_Rate,
                    ROW_NUMBER() OVER (
                        PARTITION BY I.Id, B.Id
                        ORDER BY BIUOM.IsBase DESC, IUOMP.Price DESC
                    ) AS rn
                FROM dbo.Items AS I
                    JOIN dbo.BranchItemUnitOfMeasures AS BIUOM ON BIUOM.ItemId = I.Id
                    JOIN dbo.ItemUnitOfMeasurePrices AS IUOMP ON IUOMP.BranchItemUnitOfMeasureId = BIUOM.Id
                    JOIN dbo.Branches AS B ON B.Id = BIUOM.BranchId
                    LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
                WHERE I.MaterialNumber = @MaterialNumber AND B.BranchCode = @BranchCode
            )
            SELECT TOP 1
                Full_Material_Number AS ItemCode,
                Item_Name_EN AS ItemNameEn,
                Item_Name_AR AS ItemNameAr,
                Item_Price AS UnitPrice,
                Tax_Rate AS VatPercentage
            FROM ItemPricesRanked
            WHERE rn = 1";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<UpcItemQueryResult>(
            sql, new { MaterialNumber = padded, BranchCode = cleanBranch });

        if (row == null) return null;

        return new Product
        {
            ItemCode = row.ItemCode ?? padded,
            ItemName = row.ItemNameEn ?? "",
            ItemNameAr = row.ItemNameAr,
            UnitPrice = row.UnitPrice ?? 0m,
            VatPercentage = row.VatPercentage ?? 0m,
            Quantity = 1m,
            Discount = 0m
        };
    }

    private class UpcItemQueryResult
    {
        public string? ItemCode { get; set; }
        public string? ItemNameEn { get; set; }
        public string? ItemNameAr { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? VatPercentage { get; set; }
    }
}
