using Dapper;
using OnlineOrderTool.Core;
using OnlineOrderTool.Core.DTOs;

namespace OnlineOrderTool.Data.Repositories;

/// <summary>Reads the OrderRequests table (the raw API call log) as the base
/// table for the Order Requests feature -- see docs/database-schema.md §1/§3.4
/// and remediation_plan.md B10/B11. Distinct from the older
/// UpcOrderValidationRepository, which is RequestOrderHeaders-first and never
/// reads ResponseJson/ExceptionMessage; that repository is superseded by this
/// one once a later session wires a controller to it and retires the old
/// search/details endpoints -- not touched here.</summary>
public class OrderRequestRepository : IOrderRequestRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public OrderRequestRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // OrderRequests is the base table: it is the one place RequestJson/
    // ResponseJson/ExceptionMessage live. Neither RequestOrderHeaders nor
    // Invoices is 1:1 with OrderNumber (retries and re-invoicing both create
    // extra rows), so both are joined via OUTER APPLY TOP 1 -- never a plain
    // JOIN, which would multiply a single OrderRequests row into duplicates.
    private const string HeaderApplyClause = @"
            OUTER APPLY (
                SELECT TOP 1 Id, BranchCode, BranchName, OrderStatus, ParentOrderNumber, ConsumerMobile
                FROM dbo.RequestOrderHeaders
                WHERE OrderNumber = R.OrderNumber
                ORDER BY Id DESC
            ) AS H";

    private const string InvoiceApplyClause = @"
            OUTER APPLY (
                SELECT TOP 1 Barcode, CloseDateLocalTime
                FROM dbo.Invoices
                WHERE OnlineOrderNumber = R.OrderNumber
                ORDER BY Id DESC
            ) AS I";

    private const string FromClause = @"FROM dbo.OrderRequests AS R" + HeaderApplyClause + InvoiceApplyClause;
    private const string StatsFromClause = @"FROM dbo.OrderRequests AS R" + HeaderApplyClause;
    private const int ListCommandTimeoutSeconds = 15;

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["order_date"] = "R.OrderDate",
        ["net_total"] = "R.NetTotal",
        ["item_count"] = "R.ItemCount"
    };

    /// <summary>Shared WHERE-fragment builder for ListAsync/CountAsync/StatsAsync.
    /// Every filter value is bound as a parameter. The only interpolated text
    /// anywhere in this repository is this pre-built fragment and, separately,
    /// an allowlisted ORDER BY column (ResolveSortColumn) -- never raw user
    /// input.</summary>
    internal static (string WhereSql, DynamicParameters Params) BuildFilters(OrderRequestFilters filters)
    {
        var clauses = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filters.OrderNumber))
        {
            clauses.Add("R.OrderNumber = @OrderNumber");
            p.Add("OrderNumber", filters.OrderNumber.Trim());
        }
        if (!string.IsNullOrWhiteSpace(filters.Phone))
        {
            clauses.Add("RIGHT(H.ConsumerMobile, 9) = @Phone9");
            p.Add("Phone9", Normalizers.NormalizePhoneSearch(filters.Phone));
        }
        if (!string.IsNullOrWhiteSpace(filters.BranchCode))
        {
            clauses.Add("H.BranchCode = @BranchCode");
            p.Add("BranchCode", filters.BranchCode.Trim());
        }
        if (filters.Statuses is { Count: > 0 })
        {
            // Dapper expands a list parameter into IN (@Statuses1, @Statuses2, ...)
            // automatically -- still fully parameterized, never string-built.
            clauses.Add("H.OrderStatus IN @Statuses");
            p.Add("Statuses", filters.Statuses);
        }
        else if (filters.Status.HasValue)
        {
            clauses.Add("H.OrderStatus = @Status");
            p.Add("Status", filters.Status.Value);
        }
        if (filters.Succeeded.HasValue)
        {
            clauses.Add("R.IsSucceeded = @Succeeded");
            p.Add("Succeeded", filters.Succeeded.Value);
        }
        if (filters.HasException.HasValue)
        {
            clauses.Add(filters.HasException.Value ? "R.ExceptionMessage IS NOT NULL" : "R.ExceptionMessage IS NULL");
        }
        if (filters.DateFrom.HasValue)
        {
            clauses.Add("R.OrderDate >= @DateFrom");
            p.Add("DateFrom", filters.DateFrom.Value.Date);
        }
        if (filters.DateTo.HasValue)
        {
            clauses.Add("R.OrderDate < DATEADD(day, 1, @DateTo)");
            p.Add("DateTo", filters.DateTo.Value.Date);
        }

        var whereSql = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : "";
        return (whereSql, p);
    }

    /// <summary>sort is "field" (desc) or "-field"/"+field"; unrecognized or
    /// absent falls back to R.OrderDate DESC. R.Id DESC is always appended by
    /// the caller as a stable tie-breaker -- see ListAsync.</summary>
    private static string ResolveSortColumn(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return "R.OrderDate DESC";
        var descending = !sort.StartsWith('+');
        var key = sort.TrimStart('+', '-');
        return SortColumns.TryGetValue(key, out var column)
            ? $"{column} {(descending ? "DESC" : "ASC")}"
            : "R.OrderDate DESC";
    }

    /// <summary>Builds the list query text. Internal (not private) so
    /// OrderRequestRepositoryTests can assert on the SQL shape itself --
    /// that RequestJson/ResponseJson are only ever touched via
    /// DATALENGTH/IS NULL, never selected as raw columns, and that paging is
    /// bound via @Skip/@Take rather than interpolated literals.</summary>
    internal static string BuildListSql(string whereSql, string? sort)
        => BuildListSql(whereSql, sort, applyHeaderJoinsAfterPaging: false);

    /// <summary>Builds the list query with a fast path for filters that only
    /// touch OrderRequests. The base rows are sorted and paged before the
    /// unindexed header/invoice lookups run, so the normal first page does not
    /// scan those tables once per historical request. Header-derived filters
    /// keep the original query shape because they must be applied before
    /// paging.</summary>
    internal static string BuildListSql(string whereSql, string? sort, bool applyHeaderJoinsAfterPaging)
    {
        // RequestJson/ResponseJson are never selected here -- only
        // DATALENGTH/existence, so the list stays fast at any row count.
        // GetDetailAsync is the only place the raw blobs are read.
        if (applyHeaderJoinsAfterPaging)
        {
            var orderBy = ResolveSortColumn(sort);
            return $@"
                WITH PagedRequests AS (
                    SELECT
                        R.Id, R.OrderNumber, R.OrderDate, R.NetTotal, R.ItemCount, R.IsSucceeded,
                        DATALENGTH(R.RequestJson) AS RequestBytes,
                        CAST(CASE WHEN R.ResponseJson IS NULL THEN 0 ELSE 1 END AS BIT) AS HasResponse
                    FROM dbo.OrderRequests AS R
                    {whereSql}
                    ORDER BY {orderBy}, R.Id DESC
                    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
                )
                SELECT
                    R.Id, R.OrderNumber, R.OrderDate, R.NetTotal, R.ItemCount, R.IsSucceeded,
                    R.RequestBytes, R.HasResponse,
                    H.Id AS OrderHeaderId, H.BranchCode, H.BranchName, H.OrderStatus, H.ParentOrderNumber,
                    I.Barcode AS InvoiceBarcode, I.CloseDateLocalTime AS InvoiceDate
                FROM PagedRequests AS R
                {HeaderApplyClause}
                {InvoiceApplyClause}
                ORDER BY {orderBy}, R.Id DESC";
        }

        return $@"
            SELECT
                R.Id, R.OrderNumber, R.OrderDate, R.NetTotal, R.ItemCount, R.IsSucceeded,
                DATALENGTH(R.RequestJson) AS RequestBytes,
                CAST(CASE WHEN R.ResponseJson IS NULL THEN 0 ELSE 1 END AS BIT) AS HasResponse,
                H.Id AS OrderHeaderId, H.BranchCode, H.BranchName, H.OrderStatus, H.ParentOrderNumber,
                I.Barcode AS InvoiceBarcode, I.CloseDateLocalTime AS InvoiceDate
            {FromClause}
            {whereSql}
            ORDER BY {ResolveSortColumn(sort)}, R.Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
    }

    public async Task<List<OrderRequestListItemDto>> ListAsync(
        string connectionString, OrderRequestFilters filters, int page, int pageSize, string? sort)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var (whereSql, p) = BuildFilters(filters);
        p.Add("Skip", (safePage - 1) * safePageSize);
        p.Add("Take", safePageSize);

        var sql = BuildListSql(whereSql, sort, applyHeaderJoinsAfterPaging: !RequiresHeaderJoin(filters));

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var rows = await connection.QueryAsync<ListRow>(sql, p, commandTimeout: ListCommandTimeoutSeconds);

        return rows.Select(r => new OrderRequestListItemDto(
            r.Id, r.OrderNumber ?? "", r.OrderDate, r.NetTotal ?? 0m, r.ItemCount ?? 0, r.IsSucceeded,
            r.RequestBytes, r.HasResponse,
            r.OrderHeaderId, r.BranchCode, r.BranchName, r.OrderStatus,
            r.OrderStatus.HasValue ? OrderRequestStatus.GetLabel(r.OrderStatus.Value) : null,
            r.ParentOrderNumber, r.InvoiceBarcode, r.InvoiceDate
        )).ToList();
    }

    public async Task<int> CountAsync(string connectionString, OrderRequestFilters filters)
    {
        var (whereSql, p) = BuildFilters(filters);
        var fromClause = RequiresHeaderJoin(filters) ? FromClause : @"FROM dbo.OrderRequests AS R";
        var sql = $"SELECT COUNT(*) {fromClause} {whereSql}";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, p, commandTimeout: ListCommandTimeoutSeconds);
    }

    public async Task<OrderRequestStatsDto> StatsAsync(string connectionString, OrderRequestFilters filters)
    {
        var (whereSql, p) = BuildFilters(filters);
        var sql = $@"
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN R.IsSucceeded = 1 THEN 1 ELSE 0 END) AS Succeeded,
                SUM(CASE WHEN R.IsSucceeded = 0 OR R.ExceptionMessage IS NOT NULL THEN 1 ELSE 0 END) AS Failed,
                SUM(CASE WHEN H.OrderStatus IN (6, 7) THEN 1 ELSE 0 END) AS Cancelled
            {StatsFromClause}
            {whereSql}";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QuerySingleAsync<StatsRow>(sql, p, commandTimeout: ListCommandTimeoutSeconds);
        return new OrderRequestStatsDto(row.Total, row.Succeeded ?? 0, row.Failed ?? 0, row.Cancelled ?? 0);
    }

    private static bool RequiresHeaderJoin(OrderRequestFilters filters)
        => !string.IsNullOrWhiteSpace(filters.Phone)
            || !string.IsNullOrWhiteSpace(filters.BranchCode)
            || filters.Status.HasValue
            || filters.Statuses is { Count: > 0 };

    /// <summary>The only method in this repository that reads RequestJson,
    /// ResponseJson and ExceptionMessage -- the two columns (ResponseJson,
    /// ExceptionMessage) this feature exists to surface.</summary>
    public async Task<OrderRequestDetailDto?> GetDetailAsync(string connectionString, long requestId)
    {
        using var connection = _connectionFactory.CreateConnection(connectionString);

        var request = await connection.QueryFirstOrDefaultAsync<RequestRow>(@"
            SELECT TOP 1
                R.Id, R.OrderNumber, R.OrderDate, R.NetTotal, R.ItemCount,
                R.IsSucceeded, R.ExceptionMessage, R.RequestJson, R.ResponseJson
            FROM dbo.OrderRequests AS R
            WHERE R.Id = @RequestId",
            new { RequestId = requestId });

        if (request == null) return null;

        var orderNumber = request.OrderNumber ?? "";

        var headerRow = await connection.QueryFirstOrDefaultAsync<HeaderRow>(@"
            SELECT TOP 1
                H.Id, H.OrderNumber, H.BranchCode, H.BranchName, H.OrderStatus, H.OrderDate,
                H.ConsumerMobile, H.Address, H.GrossTotal, H.NetTotal, H.TotalVat, H.TotalDiscount,
                H.OrderPaymentMethod, H.OrderNote, H.ParentOrderNumber, H.RejectionMessage
            FROM dbo.RequestOrderHeaders AS H
            WHERE H.OrderNumber = @OrderNumber
            ORDER BY H.Id DESC",
            new { OrderNumber = orderNumber });

        OrderRequestHeaderDto? header = null;
        var details = new List<OrderRequestDetailLineDto>();
        var transactions = new List<OrderRequestTransactionDto>();

        if (headerRow != null)
        {
            header = new OrderRequestHeaderDto(
                headerRow.Id, headerRow.OrderNumber ?? orderNumber, headerRow.BranchCode ?? "",
                headerRow.BranchName, headerRow.OrderStatus, OrderRequestStatus.GetLabel(headerRow.OrderStatus),
                headerRow.OrderDate, headerRow.ConsumerMobile, headerRow.Address,
                headerRow.GrossTotal ?? 0m, headerRow.NetTotal ?? 0m, headerRow.TotalVat ?? 0m,
                headerRow.TotalDiscount ?? 0m, headerRow.OrderPaymentMethod, headerRow.OrderNote,
                headerRow.ParentOrderNumber, headerRow.RejectionMessage,
                OrderRequestStatus.IsResendAllowed(headerRow.OrderStatus),
                OrderRequestStatus.IsCancelAllowed(headerRow.OrderStatus));

            var detailRows = await connection.QueryAsync<DetailLineRow>(@"
                SELECT ItemName, MaterialNumber, Quantity, UnitPrice, TotalPrice,
                       TotalDiscount, ItemVat, ItemVatPercentage, OfferCode, OfferMessage
                FROM dbo.RequestOrderDetails
                WHERE RequestOrderHeaderId = @HeaderId",
                new { HeaderId = headerRow.Id });
            details = detailRows.Select(d => new OrderRequestDetailLineDto(
                d.ItemName, d.MaterialNumber, d.Quantity ?? 0m, d.UnitPrice ?? 0m, d.TotalPrice ?? 0m,
                d.TotalDiscount ?? 0m, d.ItemVat ?? 0m, d.ItemVatPercentage ?? 0m, d.OfferCode, d.OfferMessage
            )).ToList();

            var txnRows = await connection.QueryAsync<TransactionRow>(@"
                SELECT PaymentAmount, ECommercePaymentMethod, ECommercePaymentOption,
                       OptionCommission, PaymentStatus, TransactionCode, BankCode, CardName
                FROM dbo.RequestOrderTransactions
                WHERE RequestOrderHeaderId = @HeaderId",
                new { HeaderId = headerRow.Id });
            transactions = txnRows.Select(t => new OrderRequestTransactionDto(
                t.PaymentAmount ?? 0m, t.ECommercePaymentMethod, t.ECommercePaymentOption,
                t.OptionCommission ?? 0m, t.PaymentStatus, t.TransactionCode, t.BankCode, t.CardName
            )).ToList();
        }

        var invoiceRow = await connection.QueryFirstOrDefaultAsync<InvoiceRow>(@"
            SELECT TOP 1 Barcode, CloseDateLocalTime, NetAmount, PaidAmount
            FROM dbo.Invoices
            WHERE OnlineOrderNumber = @OrderNumber
            ORDER BY Id DESC",
            new { OrderNumber = orderNumber });

        var invoice = invoiceRow == null ? null : new OrderRequestInvoiceDto(
            invoiceRow.Barcode, invoiceRow.CloseDateLocalTime, invoiceRow.NetAmount, invoiceRow.PaidAmount);

        return new OrderRequestDetailDto(
            request.Id, orderNumber, request.OrderDate, request.NetTotal ?? 0m, request.ItemCount ?? 0,
            request.IsSucceeded, request.ExceptionMessage, request.RequestJson, request.ResponseJson,
            header, details, transactions, invoice);
    }

    public async Task<List<OrderRequestAttemptDto>> ListAttemptsAsync(string connectionString, string orderNumber)
    {
        using var connection = _connectionFactory.CreateConnection(connectionString);
        var rows = await connection.QueryAsync<AttemptRow>(@"
            SELECT Id, OrderDate, IsSucceeded, ExceptionMessage
            FROM dbo.OrderRequests
            WHERE OrderNumber = @OrderNumber
            ORDER BY Id DESC",
            new { OrderNumber = orderNumber });

        return rows.Select(r => new OrderRequestAttemptDto(
            r.Id, r.OrderDate, r.IsSucceeded, !string.IsNullOrEmpty(r.ExceptionMessage)
        )).ToList();
    }

    /// <summary>parentOrderNumber (from a prior GetDetailAsync's
    /// Header.ParentOrderNumber) locates the parent; children are found by
    /// searching for headers whose ParentOrderNumber points back at
    /// orderNumber, deduped to each child's most recent header row in case a
    /// child was itself resent.</summary>
    public async Task<OrderRequestLineageDto> GetLineageAsync(string connectionString, string orderNumber, string? parentOrderNumber)
    {
        using var connection = _connectionFactory.CreateConnection(connectionString);

        OrderRequestLineageNodeDto? parent = null;
        if (!string.IsNullOrWhiteSpace(parentOrderNumber))
        {
            var parentRow = await connection.QueryFirstOrDefaultAsync<LineageNodeRow>(@"
                SELECT TOP 1 H.OrderNumber, H.OrderDate, H.OrderStatus, H.NetTotal
                FROM dbo.RequestOrderHeaders AS H
                WHERE H.OrderNumber = @ParentOrderNumber
                ORDER BY H.Id DESC",
                new { ParentOrderNumber = parentOrderNumber });

            if (parentRow != null)
            {
                parent = new OrderRequestLineageNodeDto(
                    parentRow.OrderNumber ?? parentOrderNumber, parentRow.OrderDate, parentRow.OrderStatus,
                    parentRow.OrderStatus.HasValue ? OrderRequestStatus.GetLabel(parentRow.OrderStatus.Value) : null,
                    parentRow.NetTotal);
            }
        }

        var childRows = await connection.QueryAsync<LineageNodeRow>(@"
            SELECT H.OrderNumber, H.OrderDate, H.OrderStatus, H.NetTotal
            FROM dbo.RequestOrderHeaders AS H
            WHERE H.Id IN (
                SELECT MAX(Id) FROM dbo.RequestOrderHeaders
                WHERE ParentOrderNumber = @OrderNumber
                GROUP BY OrderNumber
            )
            ORDER BY H.OrderDate DESC",
            new { OrderNumber = orderNumber });

        var children = childRows.Select(c => new OrderRequestLineageNodeDto(
            c.OrderNumber ?? "", c.OrderDate, c.OrderStatus,
            c.OrderStatus.HasValue ? OrderRequestStatus.GetLabel(c.OrderStatus.Value) : null,
            c.NetTotal
        )).ToList();

        return new OrderRequestLineageDto(parent, children);
    }

    // -- Dapper row-mapping shapes. Nullable throughout to tolerate NULLs
    // even where the schema is expected non-null, matching the defensive
    // style already used across the item/consumer repositories. --

    private class ListRow
    {
        public long Id { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal? NetTotal { get; set; }
        public int? ItemCount { get; set; }
        public bool? IsSucceeded { get; set; }
        public long RequestBytes { get; set; }
        public bool HasResponse { get; set; }
        public long? OrderHeaderId { get; set; }
        public string? BranchCode { get; set; }
        public string? BranchName { get; set; }
        public int? OrderStatus { get; set; }
        public string? ParentOrderNumber { get; set; }
        public string? InvoiceBarcode { get; set; }
        public DateTime? InvoiceDate { get; set; }
    }

    private class StatsRow
    {
        public int Total { get; set; }
        public int? Succeeded { get; set; }
        public int? Failed { get; set; }
        public int? Cancelled { get; set; }
    }

    private class RequestRow
    {
        public long Id { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal? NetTotal { get; set; }
        public int? ItemCount { get; set; }
        public bool? IsSucceeded { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? RequestJson { get; set; }
        public string? ResponseJson { get; set; }
    }

    private class HeaderRow
    {
        public long Id { get; set; }
        public string? OrderNumber { get; set; }
        public string? BranchCode { get; set; }
        public string? BranchName { get; set; }
        public int OrderStatus { get; set; }
        public DateTime OrderDate { get; set; }
        public string? ConsumerMobile { get; set; }
        public string? Address { get; set; }
        public decimal? GrossTotal { get; set; }
        public decimal? NetTotal { get; set; }
        public decimal? TotalVat { get; set; }
        public decimal? TotalDiscount { get; set; }
        public string? OrderPaymentMethod { get; set; }
        public string? OrderNote { get; set; }
        public string? ParentOrderNumber { get; set; }
        public string? RejectionMessage { get; set; }
    }

    private class DetailLineRow
    {
        public string? ItemName { get; set; }
        public string? MaterialNumber { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        public decimal? TotalDiscount { get; set; }
        public decimal? ItemVat { get; set; }
        public decimal? ItemVatPercentage { get; set; }
        public string? OfferCode { get; set; }
        public string? OfferMessage { get; set; }
    }

    private class TransactionRow
    {
        public decimal? PaymentAmount { get; set; }
        public string? ECommercePaymentMethod { get; set; }
        public string? ECommercePaymentOption { get; set; }
        public decimal? OptionCommission { get; set; }
        public string? PaymentStatus { get; set; }
        public string? TransactionCode { get; set; }
        public string? BankCode { get; set; }
        public string? CardName { get; set; }
    }

    private class InvoiceRow
    {
        public string? Barcode { get; set; }
        public DateTime? CloseDateLocalTime { get; set; }
        public decimal? NetAmount { get; set; }
        public decimal? PaidAmount { get; set; }
    }

    private class AttemptRow
    {
        public long Id { get; set; }
        public DateTime OrderDate { get; set; }
        public bool? IsSucceeded { get; set; }
        public string? ExceptionMessage { get; set; }
    }

    private class LineageNodeRow
    {
        public string? OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public int? OrderStatus { get; set; }
        public decimal? NetTotal { get; set; }
    }
}
