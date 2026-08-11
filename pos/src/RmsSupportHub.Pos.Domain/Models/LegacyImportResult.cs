namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Outcome of the one-time, non-secret legacy <c>config.json</c> import (plan section 5.5). Never
/// carries a secret — the two legacy passwords are intentionally never read by the importer.
/// </summary>
public sealed class LegacyImportResult
{
    public const int CurrentMigrationVersion = 1;

    public required bool SourceFound { get; init; }

    public required bool Succeeded { get; init; }

    public string? FailureReason { get; init; }

    public List<string> FieldsImported { get; init; } = [];

    public int MigrationVersion { get; init; } = CurrentMigrationVersion;

    public required DateTimeOffset ImportedAtUtc { get; init; }
}
