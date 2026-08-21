namespace RmsSupportHub.Api.Exceptions;

/// <summary>Base for every exception ExceptionMiddleware recognizes and
/// turns into the uniform `{ error: { code, message, details } }` envelope
/// with the matching HTTP status code (see remediation_plan.md B22).
/// Anything else that reaches the middleware is an unexpected failure and
/// maps to a generic 500 -- see ExceptionMiddleware.HandleExceptionAsync.</summary>
public abstract class ApiException : Exception
{
    protected ApiException(int statusCode, string code, string message, object? details = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    public int StatusCode { get; }
    public string Code { get; }
    public object? Details { get; }
}

public sealed class NotFoundException : ApiException
{
    public NotFoundException(string message, object? details = null)
        : base(StatusCodes.Status404NotFound, "not_found", message, details) { }
}

public sealed class BadRequestException : ApiException
{
    public BadRequestException(string message, object? details = null)
        : base(StatusCodes.Status400BadRequest, "bad_request", message, details) { }
}

public sealed class ConflictException : ApiException
{
    public ConflictException(string message, object? details = null)
        : base(StatusCodes.Status409Conflict, "conflict", message, details) { }
}

/// <summary>A downstream dependency (SQL Server, the client's RMS API)
/// failed or was unreachable. Distinct from BadRequestException -- the
/// caller's request was fine, the system it depends on was not. Maps to
/// 502 so it is never confused with this API's own bugs.</summary>
public sealed class UpstreamException : ApiException
{
    public UpstreamException(string message, object? details = null)
        : base(StatusCodes.Status502BadGateway, "upstream_error", message, details) { }
}

public sealed class FeatureNotSupportedException : ApiException
{
    public FeatureNotSupportedException(string message, object? details = null)
        : base(StatusCodes.Status501NotImplemented, "feature_not_supported", message, details) { }
}

/// <summary>Required configuration (a connection string, an environment's
/// ConnectionStringName) is missing -- a deployment/setup problem, not
/// something the caller can fix by changing their request.</summary>
public sealed class ConfigurationException : ApiException
{
    public ConfigurationException(string message, object? details = null)
        : base(StatusCodes.Status500InternalServerError, "configuration_error", message, details) { }
}

public sealed class EnvironmentNotAllowedException : ApiException
{
    public EnvironmentNotAllowedException()
        : base(StatusCodes.Status403Forbidden, "environment_not_allowed",
            "The selected environment is not allowed by this deployment.") { }
}

public sealed class EnvironmentUnconfiguredException : ApiException
{
    public EnvironmentUnconfiguredException()
        : base(StatusCodes.Status503ServiceUnavailable, "environment_unconfigured",
            "The selected environment is not configured for this deployment.") { }
}

public sealed class InvalidEnvironmentException : ApiException
{
    public InvalidEnvironmentException()
        : base(StatusCodes.Status400BadRequest, "invalid_environment",
            "The selected environment is not registered for this module.") { }
}

public sealed class CapabilityUnavailableException : ApiException
{
    public CapabilityUnavailableException()
        : base(StatusCodes.Status501NotImplemented, "capability_unavailable",
            "The requested operation is not available for this module.") { }
}

public sealed class DownstreamUnreachableException : ApiException
{
    public DownstreamUnreachableException()
        : base(StatusCodes.Status502BadGateway, "downstream_unreachable",
            "The configured downstream environment could not be reached.") { }
}

public sealed class DownstreamTimeoutException : ApiException
{
    public DownstreamTimeoutException()
        : base(StatusCodes.Status504GatewayTimeout, "downstream_timeout",
            "The configured downstream environment did not respond in time.") { }
}

public sealed class ProductionUnlockUnavailableException : ApiException
{
    public ProductionUnlockUnavailableException()
        : base(StatusCodes.Status503ServiceUnavailable, "production_unlock_unavailable",
            "Production mutations are unavailable because the owner-configured unlock secret is not provisioned.") { }
}

public sealed class ProductionUnlockFailedException : ApiException
{
    public ProductionUnlockFailedException()
        : base(StatusCodes.Status401Unauthorized, "production_unlock_failed",
            "Production unlock failed.") { }
}

public sealed class ProductionMutationLockedException : ApiException
{
    public ProductionMutationLockedException()
        : base(StatusCodes.Status423Locked, "production_mutation_locked",
            "Production mutations are locked. Unlock this module before retrying.") { }
}

public sealed class ProductionUnlockExpiredException : ApiException
{
    public ProductionUnlockExpiredException()
        : base(StatusCodes.Status401Unauthorized, "production_unlock_expired",
            "The Production unlock has expired. Unlock this module again before retrying.") { }
}
