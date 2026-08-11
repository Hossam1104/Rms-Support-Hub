namespace RmsSupportHub.Pos.Contracts.V1.Files;

/// <summary>
/// A handle is single-purpose (plan section 5.7): it can only be redeemed by the endpoint family it
/// was issued for, never reused across purposes.
/// </summary>
public enum FileHandlePurpose
{
    RestoreSource,
    BackupDestination,
}
