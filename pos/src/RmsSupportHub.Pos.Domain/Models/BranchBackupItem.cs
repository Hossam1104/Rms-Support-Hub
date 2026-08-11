using RmsSupportHub.Pos.Domain.Enums;

namespace RmsSupportHub.Pos.Domain.Models;

public sealed class BranchBackupItem(string branchCode)
{
    public string BranchCode { get; } = branchCode;

    public BranchBackupStatus Status { get; set; } = BranchBackupStatus.Pending;

    public string? RemoteZipPath { get; set; }

    public long LastObservedSizeBytes { get; set; } = -1;

    public string? LocalDownloadPath { get; set; }

    /// <summary>Stable, non-sensitive failure code for Agent-facing mapping.</summary>
    public string? FailureCode { get; set; }

    /// <summary>Opaque artifact capability assigned by the Agent after publication.</summary>
    public string? ArtifactId { get; set; }

    public string? ErrorMessage { get; set; }
}
