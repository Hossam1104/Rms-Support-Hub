namespace RmsSupportHub.Pos.Contracts.V1.Backups;

/// <summary>One selectable row of <c>GET /api/v1/backups/options</c> (branch DB, cashier DB, or an appsettings file).</summary>
public sealed record BackupComponentDto(string ComponentId, string DisplayName);
