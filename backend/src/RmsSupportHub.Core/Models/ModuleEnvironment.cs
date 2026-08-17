namespace RmsSupportHub.Core.Models;

public record DbConnectionConfig(
    string Server,
    string Database,
    string Username,
    string Password,
    string Driver
);

public record ModuleEnvironment
{
    public required string Key { get; init; }
    public required string Environment { get; init; } // "Production" | "Testing"
    public required string Description { get; init; }
    public required string Accent { get; init; }
    public required string Cue { get; init; }
    public required string Icon { get; init; }
    public required string RouteLabel { get; init; }
    public required string VisualUrl { get; init; }
    public required string VisualAlt { get; init; }
    public required bool Available { get; init; }
    public string? ApiUrl { get; init; }
    public string? CancelUrl { get; init; }
    public DbConnectionConfig? DbConfig { get; init; }

    /// <summary>Whether this registered environment needs a server-owned
    /// connection-string key before database work can start. The flag is
    /// module metadata, not caller input.</summary>
    public bool RequiresDatabase { get; init; }

    /// <summary>Whether an enabled environment must have a configured send
    /// endpoint. Read-only database-only registrations may leave this false.
    /// </summary>
    public bool RequiresApiEndpoint { get; init; }

    /// <summary>Whether an enabled environment must have a configured cancel
    /// endpoint for the module's cancel capability.</summary>
    public bool RequiresCancelEndpoint { get; init; }

    /// <summary>Server-owned health policy. These values are never bound from
    /// a browser request.</summary>
    public bool HealthProbeEnabled { get; init; }
    public TimeSpan HealthProbeTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>U1 (UI_Rework_Plan.md D3/D4): the environment every module's
    /// GetEnvironment(null) resolves to, so the default is an explicit flag
    /// rather than positional dictionary order (which silently resolved to
    /// Production because it happened to be inserted first). Exactly one
    /// environment per module should be flagged true, and it should never be
    /// the Production one.</summary>
    public bool IsDefault { get; init; }

    /// <summary>The ConnectionStrings:&lt;name&gt; key (see appsettings.json /
    /// user-secrets) this environment's database lives under. Lets
    /// LookupController/OrderRequestsController resolve a connection string
    /// per environment without comparing module-key strings.</summary>
    public string? ConnectionStringName { get; init; }

    /// <summary>Optional server-owned catalog override applied after the named
    /// connection string is resolved. This is intentionally not part of any
    /// browser DTO: an environment may reuse one credential set while routing
    /// reads to an approved catalog such as UPC Production's RmsMainProd.</summary>
    public string? DatabaseOverride { get; init; }

    public string StatusLabel => (Available, Environment) switch
    {
        (true, "Production") => "Live",
        (true, _) => "Test",
        _ => "Soon"
    };
}
