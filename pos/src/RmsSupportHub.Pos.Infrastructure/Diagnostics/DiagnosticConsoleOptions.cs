namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

public sealed class DiagnosticConsoleOptions
{
    public string OutputRoot { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DBS",
        "RmsSupportHub.Pos.Agent",
        "diagnostic-output");

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputRoot)
            || !Path.IsPathFullyQualified(OutputRoot)
            || OutputRoot.Length > 260
            || OutputRoot.Contains("..", StringComparison.Ordinal)
            || OutputRoot.Any(char.IsControl))
        {
            throw new ArgumentException("A fixed diagnostic output root is required.", nameof(OutputRoot));
        }
    }
}
