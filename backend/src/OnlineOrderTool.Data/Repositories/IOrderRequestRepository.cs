using OnlineOrderTool.Core.DTOs;

namespace OnlineOrderTool.Data.Repositories;

public interface IOrderRequestRepository
{
    Task<List<OrderRequestListItemDto>> ListAsync(
        string connectionString, OrderRequestFilters filters, int page, int pageSize, string? sort,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(string connectionString, OrderRequestFilters filters, CancellationToken cancellationToken = default);

    Task<OrderRequestStatsDto> StatsAsync(string connectionString, OrderRequestFilters filters, CancellationToken cancellationToken = default);

    /// <summary>The only method that reads RequestJson/ResponseJson/ExceptionMessage.</summary>
    Task<OrderRequestDetailDto?> GetDetailAsync(string connectionString, long requestId);

    Task<List<OrderRequestAttemptDto>> ListAttemptsAsync(string connectionString, string orderNumber);

    Task<OrderRequestLineageDto> GetLineageAsync(string connectionString, string orderNumber, string? parentOrderNumber);
}
