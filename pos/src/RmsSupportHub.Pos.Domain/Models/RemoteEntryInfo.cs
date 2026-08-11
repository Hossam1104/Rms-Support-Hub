namespace RmsSupportHub.Pos.Domain.Models;

public sealed record RemoteEntryInfo(string Name, string FullPath, DateTimeOffset CreatedAtUtc, long SizeBytes);
