namespace OnlineOrderTool.Core.Models;

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

    public string StatusLabel => (Available, Environment) switch
    {
        (true, "Production") => "Live",
        (true, _) => "Test",
        _ => "Soon"
    };
}
