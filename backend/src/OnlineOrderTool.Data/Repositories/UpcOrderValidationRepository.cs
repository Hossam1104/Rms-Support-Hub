using System.Text;
using Dapper;
using OnlineOrderTool.Core.DTOs;

namespace OnlineOrderTool.Data.Repositories;

public class UpcOrderValidationRepository : IOrderValidationRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public UpcOrderValidationRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private static string GetStatusLabel(int status) => status switch
    {
        1 => "New",
        2 => "Confirmed",
        3 => "Ready",
        4 => "With Delegate",
        5 => "Rejected",
        6 => "Canceled By Client",
        7 => "Canceled By Admin",
        8 => "Processing",
        9 => "Done",
        _ => $"Status {status}"
    };

    public async Task<IEnumerable<OrderSearchResultDto>> SearchOrdersAsync(string connectionString, OrderSearchRequest filters)
    {
        var sql = new StringBuilder(@"
            SELECT TOP 100
                H.Id AS HeaderId,
                H.OrderNumber,
                H.BranchCode,
                H.Status,
                H.CreatedDateTime AS CreationDate,
                I.Barcode AS InvoiceBarcode,
                I.CreatedDateTime AS InvoiceDate
            FROM dbo.RequestOrderHeaders AS H
            LEFT JOIN dbo.Invoices AS I ON H.OrderNumber = I.OrderNumber
            WHERE 1=1 ");

        var dynamicParams = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filters.OrderNumber))
        {
            sql.Append(" AND H.OrderNumber = @OrderNumber");
            dynamicParams.Add("OrderNumber", filters.OrderNumber.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filters.Phone))
        {
            sql.Append(" AND H.CustomerMobile = @Phone");
            dynamicParams.Add("Phone", filters.Phone.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filters.BranchCode))
        {
            sql.Append(" AND H.BranchCode = @BranchCode");
            dynamicParams.Add("BranchCode", filters.BranchCode.Trim());
        }

        if (filters.Status.HasValue)
        {
            sql.Append(" AND H.Status = @Status");
            dynamicParams.Add("Status", filters.Status.Value);
        }

        if (filters.DateFrom.HasValue)
        {
            sql.Append(" AND H.CreatedDateTime >= @DateFrom");
            dynamicParams.Add("DateFrom", filters.DateFrom.Value);
        }

        if (filters.DateTo.HasValue)
        {
            sql.Append(" AND H.CreatedDateTime <= @DateTo");
            dynamicParams.Add("DateTo", filters.DateTo.Value);
        }

        sql.Append(" ORDER BY H.Id DESC");

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var rows = await connection.QueryAsync<RawSearchResult>(sql.ToString(), dynamicParams);

        return rows.Select(r => new OrderSearchResultDto(
            HeaderId: r.HeaderId,
            OrderNumber: r.OrderNumber ?? "",
            BranchCode: r.BranchCode ?? "",
            Status: r.Status,
            StatusLabel: GetStatusLabel(r.Status),
            CreationDate: r.CreationDate,
            InvoiceBarcode: r.InvoiceBarcode,
            InvoiceDate: r.InvoiceDate,
            CanResend: r.Status is not (4 or 8 or 9)
        ));
    }

    public async Task<Dictionary<string, object?>?> GetOrderDetailsAsync(string connectionString, string orderNumber, long? headerId = null)
    {
        const string headerSql = @"
            SELECT TOP 1
                H.Id, H.OrderNumber, H.BranchCode, H.Status, H.CustomerName, H.CustomerMobile,
                H.ShippingAddress, H.DeliveryFees, H.Notes, H.CreatedDateTime, H.UpdatedDateTime
            FROM dbo.RequestOrderHeaders AS H
            WHERE (@HeaderId IS NOT NULL AND H.Id = @HeaderId) OR H.OrderNumber = @OrderNumber
            ORDER BY H.Id DESC";

        const string detailsSql = @"
            SELECT ItemCode, ItemName, Quantity, UnitPrice, DiscountAmount, VatAmount, LineTotal
            FROM dbo.RequestOrderDetails
            WHERE RequestOrderHeaderId = @HeaderId";

        const string txSql = @"
            SELECT PaymentMethod, PaymentStatus, Amount, TransactionId
            FROM dbo.RequestOrderTransactions
            WHERE RequestOrderHeaderId = @HeaderId";

        const string invoiceSql = @"
            SELECT TOP 1 Barcode, NetAmount, CreatedDateTime
            FROM dbo.Invoices
            WHERE OrderNumber = @OrderNumber";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var header = await connection.QueryFirstOrDefaultAsync<dynamic>(headerSql, new { HeaderId = headerId, OrderNumber = orderNumber });

        if (header == null) return null;

        long actualHeaderId = header.Id;
        string actualOrderNumber = header.OrderNumber ?? orderNumber;

        var details = await connection.QueryAsync<dynamic>(detailsSql, new { HeaderId = actualHeaderId });
        var transactions = await connection.QueryAsync<dynamic>(txSql, new { HeaderId = actualHeaderId });
        var invoice = await connection.QueryFirstOrDefaultAsync<dynamic>(invoiceSql, new { OrderNumber = actualOrderNumber });

        return new Dictionary<string, object?>
        {
            ["header"] = header,
            ["details"] = details,
            ["transactions"] = transactions,
            ["invoice"] = invoice,
            ["canResend"] = (int)header.Status is not (4 or 8 or 9)
        };
    }

    public async Task<string?> GetLatestRequestJsonAsync(string connectionString, string orderNumber)
    {
        const string sql = @"
            SELECT TOP 1 RequestJson
            FROM dbo.OrderRequests
            WHERE OrderNumber = @OrderNumber
            ORDER BY Id DESC";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        return await connection.QueryFirstOrDefaultAsync<string>(sql, new { OrderNumber = orderNumber });
    }

    private class RawSearchResult
    {
        public long HeaderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? BranchCode { get; set; }
        public int Status { get; set; }
        public DateTime CreationDate { get; set; }
        public string? InvoiceBarcode { get; set; }
        public DateTime? InvoiceDate { get; set; }
    }
}
