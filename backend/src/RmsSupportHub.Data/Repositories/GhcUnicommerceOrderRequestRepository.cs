using System.Text.Json;
using Dapper;
using RmsSupportHub.Core.DTOs;

namespace RmsSupportHub.Data.Repositories;

/// <summary>
/// Bounded, read-only history adapter for the verified Uni-Commerce schema.
/// ExternalInvoiceRequests is an integration-attempt table, not a drop-in
/// replacement for OrderRequests: it has no branch, business status, totals,
/// line, or cancellation columns. The adapter therefore exposes only the
/// common request-attempt shape and leaves cancel/resend disabled in the
/// module capabilities.
/// </summary>
public interface IGhcUnicommerceOrderRequestRepository : IOrderRequestRepository
{
}

public sealed class GhcUnicommerceOrderRequestRepository : IGhcUnicommerceOrderRequestRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    public GhcUnicommerceOrderRequestRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const int ListCommandTimeoutSeconds = 15;

    internal static (string WhereSql, DynamicParameters Params) BuildFilters(OrderRequestFilters filters)
    {
        var clauses = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filters.OrderNumber))
        {
            var orderNumber = filters.OrderNumber.Trim();
            if (filters.ExactOrderNumber)
            {
                clauses.Add("R.ReferenceNumber = @OrderNumber");
                parameters.Add("OrderNumber", orderNumber);
            }
            else
            {
                clauses.Add("R.ReferenceNumber LIKE @OrderNumberPattern ESCAPE '\\'");
                parameters.Add("OrderNumberPattern", $"%{EscapeLikePattern(orderNumber)}%");
            }
        }

        if (filters.Succeeded.HasValue)
        {
            clauses.Add("R.Success = @Succeeded");
            parameters.Add("Succeeded", filters.Succeeded.Value);
        }

        if (filters.HasException.HasValue)
        {
            // Uni-Commerce records a message for every attempt. The bounded
            // common contract treats an unsuccessful attempt as the adapter's
            // exception/failure state.
            clauses.Add("R.Success = @HasExceptionSuccess");
            parameters.Add("HasExceptionSuccess", !filters.HasException.Value);
        }

        if (filters.DateFrom.HasValue)
        {
            clauses.Add("R.RequestUtcDate >= @DateFrom");
            parameters.Add("DateFrom", filters.DateFrom.Value.Date);
        }

        if (filters.DateTo.HasValue)
        {
            clauses.Add("R.RequestUtcDate < DATEADD(day, 1, @DateTo)");
            parameters.Add("DateTo", filters.DateTo.Value.Date);
        }

        return (clauses.Count == 0 ? "" : "WHERE " + string.Join(" AND ", clauses), parameters);
    }

    internal static string BuildListSql(string whereSql, string? sort = null)
    {
        var descending = string.IsNullOrWhiteSpace(sort) || !sort.TrimStart().StartsWith('+');
        var orderDirection = descending ? "DESC" : "ASC";
        return $@"
        SELECT
            R.Id,
            R.ReferenceNumber AS OrderNumber,
            R.RequestUtcDate AS OrderDate,
            CAST(0 AS decimal(18, 2)) AS NetTotal,
            CAST(0 AS int) AS ItemCount,
            R.Success AS IsSucceeded,
            DATALENGTH(R.RequestJson) AS RequestBytes,
            CAST(1 AS bit) AS HasResponse
        FROM dbo.ExternalInvoiceRequests AS R
        {whereSql}
        ORDER BY R.RequestUtcDate {orderDirection}, R.Id {orderDirection}
        OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
    }

    internal static string BuildCountSql(string whereSql) => $@"
        SELECT COUNT(*)
        FROM dbo.ExternalInvoiceRequests AS R
        {whereSql}";

    internal static string BuildStatsSql(string whereSql) => $@"
        SELECT
            COUNT(*) AS Total,
            COALESCE(SUM(CASE WHEN R.Success = 1 THEN 1 ELSE 0 END), 0) AS Succeeded,
            COALESCE(SUM(CASE WHEN R.Success = 0 THEN 1 ELSE 0 END), 0) AS Failed,
            CAST(0 AS int) AS Cancelled
        FROM dbo.ExternalInvoiceRequests AS R
        {whereSql}";

    public async Task<List<OrderRequestListItemDto>> ListAsync(
        string connectionString, OrderRequestFilters filters, int page, int pageSize,
        string? sort, CancellationToken cancellationToken = default)
    {
        var (whereSql, parameters) = BuildFilters(filters);
        parameters.Add("Skip", (long)(Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 200));
        parameters.Add("Take", Math.Clamp(pageSize, 1, 200));

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var command = new CommandDefinition(
            BuildListSql(whereSql, sort), parameters,
            commandTimeout: ListCommandTimeoutSeconds,
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<ListRow>(command);

        return rows.Select(row => new OrderRequestListItemDto(
            row.Id,
            row.OrderNumber ?? "",
            row.OrderDate,
            row.NetTotal ?? 0m,
            row.ItemCount ?? 0,
            row.IsSucceeded,
            row.RequestBytes,
            row.HasResponse,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null)).ToList();
    }

    public async Task<int> CountAsync(
        string connectionString, OrderRequestFilters filters,
        CancellationToken cancellationToken = default)
    {
        var (whereSql, parameters) = BuildFilters(filters);
        using var connection = _connectionFactory.CreateConnection(connectionString);
        var command = new CommandDefinition(
            BuildCountSql(whereSql), parameters,
            commandTimeout: ListCommandTimeoutSeconds,
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<OrderRequestStatsDto> StatsAsync(
        string connectionString, OrderRequestFilters filters,
        CancellationToken cancellationToken = default)
    {
        var (whereSql, parameters) = BuildFilters(filters);
        using var connection = _connectionFactory.CreateConnection(connectionString);
        var command = new CommandDefinition(
            BuildStatsSql(whereSql), parameters,
            commandTimeout: ListCommandTimeoutSeconds,
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleAsync<StatsRow>(command);
        return new OrderRequestStatsDto(
            row.Total, row.Succeeded ?? 0, row.Failed ?? 0, row.Cancelled ?? 0);
    }

    public async Task<OrderRequestDetailDto?> GetDetailAsync(string connectionString, long requestId)
    {
        const string sql = @"
            SELECT Id, ReferenceNumber, RequestJson, Success, Message,
                   RequestUtcDate, ExternalInvoiceId
            FROM dbo.ExternalInvoiceRequests
            WHERE Id = @RequestId";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<DetailRow>(sql, new { RequestId = requestId });
        if (row is null) return null;

        var responseJson = JsonSerializer.Serialize(new
        {
            message = row.Message,
            externalInvoiceId = row.ExternalInvoiceId
        });

        return new OrderRequestDetailDto(
            row.Id,
            row.ReferenceNumber ?? "",
            row.RequestUtcDate,
            0m,
            0,
            row.Success,
            row.Success ? null : row.Message,
            row.RequestJson,
            responseJson,
            null,
            new List<OrderRequestDetailLineDto>(),
            new List<OrderRequestTransactionDto>(),
            null);
    }

    public async Task<List<OrderRequestAttemptDto>> ListAttemptsAsync(
        string connectionString, string orderNumber)
    {
        const string sql = @"
            SELECT Id, RequestUtcDate AS OrderDate, Success AS IsSucceeded
            FROM dbo.ExternalInvoiceRequests
            WHERE ReferenceNumber = @OrderNumber
            ORDER BY Id DESC";

        using var connection = _connectionFactory.CreateConnection(connectionString);
        var rows = await connection.QueryAsync<AttemptRow>(sql, new { OrderNumber = orderNumber });
        return rows.Select(row => new OrderRequestAttemptDto(
            row.Id, row.OrderDate, row.IsSucceeded, row.IsSucceeded != true)).ToList();
    }

    public Task<OrderRequestLineageDto> GetLineageAsync(
        string connectionString, string orderNumber, string? parentOrderNumber) =>
        Task.FromResult(new OrderRequestLineageDto(
            Parent: null,
            Children: new List<OrderRequestLineageNodeDto>()));

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal)
        .Replace("[", "\\[", StringComparison.Ordinal);

    private sealed class ListRow
    {
        public long Id { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal? NetTotal { get; set; }
        public int? ItemCount { get; set; }
        public bool? IsSucceeded { get; set; }
        public long RequestBytes { get; set; }
        public bool HasResponse { get; set; }
    }

    private sealed class StatsRow
    {
        public int Total { get; set; }
        public int? Succeeded { get; set; }
        public int? Failed { get; set; }
        public int? Cancelled { get; set; }
    }

    private sealed class DetailRow
    {
        public long Id { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? RequestJson { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public DateTime RequestUtcDate { get; set; }
        public long? ExternalInvoiceId { get; set; }
    }

    private sealed class AttemptRow
    {
        public long Id { get; set; }
        public DateTime OrderDate { get; set; }
        public bool? IsSucceeded { get; set; }
    }
}
