using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>
/// One-time, non-secret importer for the legacy <c>%USERPROFILE%\.pos_admin_tool\config.json</c>
/// (plan section 5.5). Never reads or migrates the legacy SQL/RDB passwords, and never modifies,
/// rewrites, or deletes the legacy file. Idempotent: a second call returns the recorded result of
/// the first without importing again.
/// </summary>
public interface ILegacyConfigurationImporter
{
    Task<LegacyImportResult> ImportAsync(CancellationToken cancellationToken = default);
}
