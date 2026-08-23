using System.IO;
using System.Security;
using RmsSupportHub.Pos.Contracts.V1.Artifacts;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Desktop.Wpf.Models;
using RmsSupportHub.Pos.LocalIpc;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public sealed class LocalAgentSupportBundleClient(LocalIpcClient localIpcClient)
    : ILocalSupportBundleClient
{
    private const long MaximumBundleBytes = 8L * 1024 * 1024;

    public async Task<SupportBundleResult> GenerateAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await localIpcClient
                .GenerateSupportBundleAsync(correlationId, cancellationToken)
                .ConfigureAwait(false);

            if (!response.Succeeded)
            {
                return MapFailure(response.ErrorCode, response.CorrelationId);
            }

            if (response.Result is not { } bundle
                || !string.Equals(bundle.CorrelationId, response.CorrelationId, StringComparison.Ordinal)
                || bundle.CreatedAtUtc == default
                || bundle.IncludedSections is null
                || bundle.IncludedSections.Count is < 1 or > 32
                || bundle.IncludedSections.Any(section => !IsSafeText(section, 128)))
            {
                return InvalidResponse(response.CorrelationId);
            }

            if (!TryValidateArtifact(bundle.Artifact, bundle.CreatedAtUtc))
            {
                return InvalidResponse(response.CorrelationId);
            }

            return SupportBundleResult.Succeeded(
                bundle.Artifact,
                bundle.CreatedAtUtc,
                bundle.CorrelationId,
                bundle.IncludedSections.ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return SupportBundleResult.Failure(
                SupportBundleViewState.TimedOut,
                "support_bundle_timeout",
                "Support Bundle generation timed out.",
                correlationId);
        }
        catch (LocalIpcProtocolMismatchException)
        {
            return SupportBundleResult.Failure(
                SupportBundleViewState.ProtocolMismatch,
                "protocol_mismatch",
                "Desktop and Agent versions are not compatible.",
                correlationId);
        }
        catch (LocalIpcServerIdentityException)
        {
            return SupportBundleResult.Failure(
                SupportBundleViewState.SecurityVerificationFailed,
                "security_verification_failed",
                "The local Agent connection could not be verified.",
                correlationId);
        }
        catch (LocalIpcProtocolException)
        {
            return InvalidResponse(correlationId);
        }
        catch (UnauthorizedAccessException)
        {
            return SecurityFailure(correlationId);
        }
        catch (SecurityException)
        {
            return SecurityFailure(correlationId);
        }
        catch (IOException)
        {
            return SupportBundleResult.Failure(
                SupportBundleViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId);
        }
        catch (Exception)
        {
            return SupportBundleResult.Failure(
                SupportBundleViewState.Failed,
                "support_bundle_failed",
                "The Support Bundle could not be generated.",
                correlationId);
        }
    }

    private static SupportBundleResult MapFailure(string? errorCode, string correlationId) =>
        errorCode?.Trim().ToLowerInvariant() switch
        {
            "administrator_authorization_required" => SupportBundleResult.Failure(
                SupportBundleViewState.Unauthorized,
                "administrator_authorization_required",
                "Administrator authority is required to generate a Support Bundle.",
                correlationId),
            "audit_unavailable" => SupportBundleResult.Failure(
                SupportBundleViewState.AuditUnavailable,
                "audit_unavailable",
                "Support Bundle generation is unavailable because the audit record could not be persisted.",
                correlationId),
            "support_bundle_timeout" or "request_timeout" => SupportBundleResult.Failure(
                SupportBundleViewState.TimedOut,
                "support_bundle_timeout",
                "Support Bundle generation timed out.",
                correlationId),
            "protocol_mismatch" or "unsupported_protocol_version" or "unknown_operation" => SupportBundleResult.Failure(
                SupportBundleViewState.ProtocolMismatch,
                "protocol_mismatch",
                "Desktop and Agent versions are not compatible.",
                correlationId),
            "security_verification_failed" or "unauthorized" => SecurityFailure(correlationId),
            "invalid_response" or "malformed_response" => InvalidResponse(correlationId),
            "agent_unavailable" => SupportBundleResult.Failure(
                SupportBundleViewState.Unavailable,
                "agent_unavailable",
                "RMS Support Agent is not available on this machine.",
                correlationId),
            "support_bundle_unavailable" => SupportBundleResult.Failure(
                SupportBundleViewState.Unavailable,
                "support_bundle_unavailable",
                "The Support Bundle could not be generated.",
                correlationId),
            _ => SupportBundleResult.Failure(
                SupportBundleViewState.Failed,
                "support_bundle_failed",
                "The Support Bundle could not be generated.",
                correlationId)
        };

    private static SupportBundleResult InvalidResponse(string correlationId) => SupportBundleResult.Failure(
        SupportBundleViewState.InvalidResponse,
        "invalid_response",
        "The Agent returned an invalid Support Bundle response.",
        correlationId);

    private static SupportBundleResult SecurityFailure(string correlationId) => SupportBundleResult.Failure(
        SupportBundleViewState.SecurityVerificationFailed,
        "security_verification_failed",
        "The local Agent connection could not be verified.",
        correlationId);

    private static bool TryValidateArtifact(ArtifactMetadataDto? artifact, DateTimeOffset createdAtUtc)
    {
        if (artifact is null
            || artifact.ArtifactId.Length != 32
            || !artifact.ArtifactId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
            || !IsSafeText(artifact.DisplayName, 128)
            || artifact.SizeBytes <= 0
            || artifact.SizeBytes > MaximumBundleBytes
            || artifact.CreatedAtUtc == default
            || artifact.CreatedAtUtc > createdAtUtc
            || artifact.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= artifact.CreatedAtUtc
            || !IsSafeChecksum(artifact.Sha256Checksum))
        {
            return false;
        }

        return true;
    }

    private static bool IsSafeChecksum(string? value) =>
        string.Equals(value, "unavailable", StringComparison.Ordinal)
        || value is { Length: 64 }
            && value.All(Uri.IsHexDigit);

    private static bool IsSafeText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || value.Any(char.IsControl)
            || value.Contains('\\')
            || value.Contains('/')
            || value.Contains("connectionstring", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || value.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
