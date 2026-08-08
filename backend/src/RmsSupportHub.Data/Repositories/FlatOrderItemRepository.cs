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

    /// <summary>GHC item lookup by 6-digit material number. UNVERIFIED --
    /// GHC database credentials have never been confirmed against a live
    /// database; this is the best available reference, ported from
    /// _legacy_flask/modules/flat_order.py::lookup_item (itself marked
    /// "still-guessed" in that source). See docs/database-schema.md §3.1.
    ///
    /// branchCode is accepted for IItemRepository parity with UpcItemRepository
    /// but unused -- GHC pricing in the legacy source is not branch-specific.
    ///
    /// The legacy query also supports optional customer_number / sap_tax_code /
    /// sap_mat_generic filters (an EXISTS(dbo.Customers ...) check and two
    /// I.SapTaxCode / I.SapMatGeneric equality filters). They are not wired
    /// here: IItemRepository.LookupItemAsync has no parameters for them today,
    /// and nothing in LookupController currently sends them either. Adding
    /// them cleanly needs a small interface change (an optional filters
    /// parameter) threaded through LookupController -- left for a follow-up
    /// session rather than changing the shared interface's shape here.</summary>
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
                INNER JOIN dbo.ItemUnitOfMeasures AS IUM ON I.Id = IUM.ItemId
                INNER JOIN dbo.ItemUnitOfMeasureBarCodes AS IUOMB ON IUM.Id = IUOMB.ItemUnitOfMeasureId
                LEFT JOIN dbo.ItemPrices AS IP ON IUM.Id = IP.ItemUnitOfMeasureId
            WHERE RIGHT(I.MaterialNumber, 6) = @MaterialNumber
              AND IP.IsActive = 1
              AND IP.Price IS NOT NULL
              AND IP.ToDate > GETDATE()
            ORDER BY I.Id DESC";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QueryFirstOrDefaultAsync<GhcItemQueryResult>(sql, new { MaterialNumber = materialNumber });

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

    private class GhcItemQueryResult
    {
        public string? ItemCode { get; set; }
        public string? ItemBarcode { get; set; }
        public string? ItemNameEn { get; set; }
        public string? ItemNameAr { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? VatPercentage { get; set; }
    }
}
