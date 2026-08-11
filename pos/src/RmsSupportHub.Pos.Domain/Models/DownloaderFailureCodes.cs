namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>Stable downloader failure vocabulary. Raw HTTP/SMB/Win32 details never use this boundary.</summary>
public static class DownloaderFailureCodes
{
    public const string InvalidConfiguration = "downloader.invalid_configuration";
    public const string InvalidBranch = "downloader.branch_invalid";
    public const string NoBranches = "downloader.no_branches";
    public const string CredentialMissing = "downloader.credential_missing";
    public const string EndpointRejected = "downloader.endpoint_rejected";
    public const string TriggerFailed = "downloader.trigger_failed";
    public const string TriggerOutcomeUnknown = "downloader.trigger_outcome_unknown";
    public const string BatchFolderTimeout = "downloader.batch_folder_timeout";
    public const string ZipTimeout = "downloader.zip_timeout";
    public const string StableSizeTimeout = "downloader.stable_size_timeout";
    public const string DownloadFailed = "downloader.download_failed";
    public const string DownloadCancelled = "downloader.download_cancelled";
    public const string SmbRootRejected = "downloader.smb_root_rejected";
    public const string SmbConnectionFailed = "downloader.smb_connection_failed";
    public const string SmbRepositoryFailed = "downloader.smb_repository_failed";
    public const string ArtifactCatalogFull = "downloader.artifact_catalog_full";
    public const string ArtifactPublicationFailed = "downloader.artifact_publication_failed";
    public const string Timeout = "downloader.timeout";
    public const string PartialFailure = "downloader.partial_failure";
    public const string CancelledAfterPartial = "downloader.cancelled_after_partial";
}

public static class DownloaderOperatorGuidance
{
    public const string TriggerOutcomeUnknown =
        "The remote backup trigger outcome is unknown. Check the remote backup state before retrying.";
}
