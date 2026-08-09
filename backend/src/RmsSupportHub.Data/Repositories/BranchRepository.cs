using Dapper;
using RmsSupportHub.Core;
using RmsSupportHub.Core.DTOs;
using RmsSupportHub.Core.Repositories;

namespace RmsSupportHub.Data.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly ISqlServerConnectionFactory _connectionFactory;

    internal const string ListBranchesSql = @"
            SELECT B.BranchCode, B.Name
            FROM dbo.Branches AS B
            WHERE B.IsActive = 1
              AND B.IsDeleted = 0
              AND B.BranchCode IS NOT NULL
              AND B.BranchCode <> ''
            ORDER BY B.Id";

    public BranchRepository(ISqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>Branch picker source: dbo.Branches itself, using ONLY the
    /// columns U0 verified live against RmsMainTest2 (docs/database-schema.md
    /// §1, 2026-07-26): BranchCode (nullable), Name, IsActive, IsDeleted.
    /// Filters IsActive = 1 (the confirmed active flag) and IsDeleted = 0;
    /// IsTestingBranch is deliberately NOT filtered out -- the Testing lane
    /// legitimately works against testing branches, and hiding them would
    /// silently break exactly the verification this tool exists for.
    /// Results follow the database branch identity (B.Id) so every picker
    /// receives the same stable branch order.
    /// Replaces the RequestOrderHeaders GROUP BY in OrderRequestRepository,
    /// which could only see branches that already had orders and whose name
    /// was a MAX() over denormalised history (UI_Rework_Plan.md D7).
    /// Parameterised throughout -- no user input is interpolated into the
    /// SQL at all.</summary>
    public async Task<List<BranchOptionDto>> ListBranchesAsync(string connectionString)
    {
        using var connection = _connectionFactory.CreateConnection(connectionString);
        var rows = await connection.QueryAsync<BranchRow>(ListBranchesSql);

        return rows.Select(r => new BranchOptionDto(r.BranchCode ?? "", r.Name ?? "")).ToList();
    }

    private class BranchRow
    {
        public string? BranchCode { get; set; }
        public string? Name { get; set; }
    }
}
