using RmsSupportHub.Core.Models;

namespace RmsSupportHub.Core.Repositories;

public interface IItemRepository
{
    Task<Product?> LookupItemAsync(string connectionString, string materialNumber, string? branchCode = null);
}

/// <summary>Marker interfaces so ModuleRegistry's constructor can have
/// FlatOrderItemRepository/UpcItemRepository injected unambiguously by type
/// (plain DI, no keyed-service plumbing needed) despite both implementing
/// the same base IItemRepository. Declared in Core (not Data) specifically
/// so GhcEcommerceModule/UpcEcommerceModule/ModuleRegistry -- which live in
/// Core -- never need to reference RmsSupportHub.Data, which would create
/// a circular project reference (Data already references Core).</summary>
public interface IGhcItemRepository : IItemRepository { }
public interface IUpcItemRepository : IItemRepository { }
