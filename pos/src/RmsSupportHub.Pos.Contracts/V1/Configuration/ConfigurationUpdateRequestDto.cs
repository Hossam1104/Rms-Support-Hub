namespace RmsSupportHub.Pos.Contracts.V1.Configuration;

/// <summary>
/// <c>PUT /api/v1/configuration</c> request. <see cref="SqlPassword"/> is write-only and
/// omitted/null means "keep the current secret" (plan section 5.5). Clearing a secret is a
/// separate, explicitly authorized operation (see <see cref="ClearSecretRequestDto"/>), never a
/// side effect of this endpoint. <see cref="ExpectedVersion"/> is the optimistic-concurrency token
/// from the last <see cref="RedactedConfigurationDto.Version"/> read.
/// </summary>
public sealed record ConfigurationUpdateRequestDto(
    string SqlInstance,
    string SqlUser,
    string? SqlPassword,
    string BranchCode,
    string PosNumber,
    string Release,
    string ClientName,
    string ApiBaseUrl,
    IReadOnlyList<string> Databases,
    IReadOnlyList<string> Services,
    DownloaderConfigurationUpdateRequestDto Downloader,
    long ExpectedVersion);
