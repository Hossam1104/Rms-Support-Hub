using OnlineOrderTool.Core.DTOs;
using OnlineOrderTool.Core.Models;

namespace OnlineOrderTool.Data.Repositories;

public interface IOrderValidationRepository
{
    Task<IEnumerable<OrderSearchResultDto>> SearchOrdersAsync(string connectionString, OrderSearchRequest filters);
    Task<Dictionary<string, object?>?> GetOrderDetailsAsync(string connectionString, string orderNumber, long? headerId = null);
    Task<string?> GetLatestRequestJsonAsync(string connectionString, string orderNumber);
}
