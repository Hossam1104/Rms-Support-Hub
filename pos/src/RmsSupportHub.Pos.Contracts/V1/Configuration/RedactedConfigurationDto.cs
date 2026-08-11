namespace RmsSupportHub.Pos.Contracts.V1.Configuration;

/// <summary>
/// <c>GET /api/v1/configuration</c> response. Carries <see cref="HasSqlPassword"/> in place of a
/// secret value — the SQL password itself is never returned to the browser (plan section 5.5).
/// </summary>
public sealed record RedactedConfigurationDto(
    string SqlInstance,
    string SqlUser,
    bool HasSqlPassword,
    string BranchCode,
    string PosNumber,
    string Release,
    string ClientName,
    string ApiBaseUrl,
    IReadOnlyList<string> Databases,
    IReadOnlyList<string> Services,
    RedactedDownloaderConfigurationDto Downloader,
    long Version);
