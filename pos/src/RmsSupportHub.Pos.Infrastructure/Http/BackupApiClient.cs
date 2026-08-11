using System.Net.Http.Headers;
using System.Net.Http.Json;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Http;

/// <summary>
/// Triggers only the server-configured RMS endpoint. Automatic redirects are intentionally not
/// trusted; every redirect is inspected by the same endpoint policy before another request is sent.
/// </summary>
public sealed class BackupApiClient : IBackupApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHostAddressResolver _addressResolver;

    public BackupApiClient(HttpClient httpClient, IHostAddressResolver? addressResolver = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _addressResolver = addressResolver ?? new SystemHostAddressResolver();
    }

    public async Task<DownloaderTriggerResult> TriggerBackupAsync(
        string apiUrl,
        IReadOnlyList<string> branchCodes,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> normalizedBranches;
        try
        {
            normalizedBranches = DownloaderInputPolicy.NormalizeBranchCodes(branchCodes);
        }
        catch (ArgumentException)
        {
            return NotAttempted(DownloaderFailureCodes.InvalidBranch);
        }

        BackupApiEndpointPolicy policy;
        Uri current;
        try
        {
            policy = BackupApiEndpointPolicy.FromConfiguredEndpoint(apiUrl);
            current = policy.ValidateInitial(apiUrl);
            await policy.ValidateResolvedAddressesAsync(current, _addressResolver, cancellationToken).ConfigureAwait(false);
        }
        catch (BackupApiPolicyException exception)
        {
            return NotAttempted(exception.Code);
        }
        catch (OperationCanceledException)
        {
            return NotAttempted();
        }
        catch
        {
            return NotAttempted(DownloaderFailureCodes.EndpointRejected);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(policy.RequestTimeout);
        var anyDispatchBegun = false;

        for (var redirect = 0; ; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, current)
            {
                Content = JsonContent.Create(normalizedBranches)
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

            HttpResponseMessage response;
            var dispatchBeforeRequest = anyDispatchBegun;
            try
            {
                timeout.Token.ThrowIfCancellationRequested();
                anyDispatchBegun = true;
                response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return anyDispatchBegun ? Unknown() : NotAttempted();
            }
            catch (Exception exception)
            {
                if (TryFind(exception, out BackupApiPolicyException? policyException))
                {
                    return dispatchBeforeRequest
                        ? Unknown()
                        : NotAttempted(policyException!.Code);
                }

                if (TryFind(exception, out BackupApiRequestException? requestException))
                {
                    return requestException!.TriggerState == DownloaderTriggerState.Failed
                        ? Failed(requestException.Code)
                        : Unknown();
                }

                return anyDispatchBegun ? Unknown() : NotAttempted(DownloaderFailureCodes.TriggerFailed);
            }

            using (response)
            {
                // This also detects a client configured with an unsafe automatic redirect handler
                // after the fact; production registration disables automatic redirects entirely.
                if (response.RequestMessage?.RequestUri is { } actualUri
                    && !string.Equals(actualUri.AbsoluteUri, current.AbsoluteUri, StringComparison.Ordinal))
                {
                    return Unknown();
                }

                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (redirect >= policy.MaxRedirects || response.Headers.Location is not { } location)
                    {
                        return Unknown();
                    }

                    try
                    {
                        current = policy.ValidateRedirect(current, location);
                        await policy.ValidateResolvedAddressesAsync(current, _addressResolver, timeout.Token).ConfigureAwait(false);
                    }
                    catch
                    {
                        // The first POST already received a response. Even when the next target is
                        // rejected safely, the Agent cannot prove whether the remote side effect
                        // occurred, so do not collapse this into a definitive rejection.
                        return Unknown();
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    // The POST has been dispatched, but the repository has no authoritative RMS
                    // response contract proving that a non-success status is side-effect-free.
                    // Status-code conventions alone cannot establish that the remote job was not
                    // created, so preserve the truthful unknown outcome and stop the workflow.
                    return Unknown();
                }

                if (response.Content.Headers.ContentLength is > 64 * 1024)
                {
                    return Unknown();
                }

                return new DownloaderTriggerResult(DownloaderTriggerState.Accepted);
            }
        }
    }

    private static DownloaderTriggerResult NotAttempted(string? failureCode = null) =>
        new(DownloaderTriggerState.NotAttempted, failureCode);

    private static DownloaderTriggerResult Failed(string failureCode) =>
        new(DownloaderTriggerState.Failed, failureCode);

    private static DownloaderTriggerResult Unknown() =>
        new(DownloaderTriggerState.OutcomeUnknown, DownloaderFailureCodes.TriggerOutcomeUnknown);

    private static bool TryFind<T>(Exception exception, out T? match)
        where T : Exception
    {
        if (exception is T typed)
        {
            match = typed;
            return true;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                if (TryFind(inner, out match))
                {
                    return true;
                }
            }
        }
        else if (exception.InnerException is not null && TryFind(exception.InnerException, out match))
        {
            return true;
        }

        match = null;
        return false;
    }
}

public sealed class BackupApiRequestException(
    string code,
    DownloaderTriggerState triggerState = DownloaderTriggerState.OutcomeUnknown)
    : DownloaderTriggerException(code, triggerState)
{
}
