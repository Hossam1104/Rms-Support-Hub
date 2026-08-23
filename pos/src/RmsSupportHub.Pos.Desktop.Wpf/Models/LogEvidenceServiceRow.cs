using RmsSupportHub.Pos.Contracts.V1.Diagnostics;

namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public sealed record LogEvidenceRecommendationRow(
    string Code,
    string Label,
    string Summary);

public sealed record LogEvidenceServiceRow(
    string ServiceId,
    string DisplayName,
    FailureCategory Category,
    FailureSeverity Severity,
    FailureConfidence Confidence,
    string Summary,
    IReadOnlyList<LogEvidenceRecordRow> Records,
    IReadOnlyList<string> UnknownReasons,
    IReadOnlyList<LogEvidenceRecommendationRow> Recommendations)
{
    public string CategoryLabel => Category switch
    {
        FailureCategory.None => "No failure evidence",
        FailureCategory.ServiceStopped => "Service stopped",
        FailureCategory.ServiceStartFailure => "Service start failure",
        FailureCategory.Crash => "Crash evidence",
        FailureCategory.Database => "Database evidence",
        FailureCategory.Network => "Network evidence",
        FailureCategory.Configuration => "Configuration evidence",
        FailureCategory.VersionDrift => "Version drift",
        _ => "Unknown"
    };

    public string SeverityLabel => Severity switch
    {
        FailureSeverity.Informational => "Informational",
        FailureSeverity.Warning => "Warning",
        FailureSeverity.ActionRequired => "Action required",
        _ => "Unknown"
    };

    public string ConfidenceLabel => Confidence switch
    {
        FailureConfidence.High => "High confidence",
        FailureConfidence.Medium => "Medium confidence",
        FailureConfidence.Low => "Low confidence",
        _ => "Unknown confidence"
    };

    public string RecordCountDisplay => Records.Count == 1
        ? "1 bounded record"
        : $"{Records.Count} bounded records";

    public string UnknownReasonsDisplay => UnknownReasons.Count == 0
        ? "No unknown source reasons"
        : string.Join("  |  ", UnknownReasons);

    public string RecommendationDisplay => Recommendations.Count == 0
        ? "No action guidance"
        : string.Join("  |  ", Recommendations.Select(item => item.Label));

    public static bool TryCreate(
        ServiceFailureAnalysisDto item,
        out LogEvidenceServiceRow? row)
    {
        row = null;
        if (string.IsNullOrWhiteSpace(item.ServiceId)
            || !IsSafeText(item.ServiceId, 64)
            || !IsSafeText(item.ServiceDisplayName, 128)
            || !Enum.IsDefined(item.Category)
            || !Enum.IsDefined(item.Severity)
            || !Enum.IsDefined(item.Confidence)
            || item.CheckedAtUtc == default
            || !IsSafeText(item.Summary, 512)
            || item.Evidence is null
            || item.Evidence.Count > 12
            || item.UnknownReasons is null
            || item.UnknownReasons.Count > 8
            || item.Recommendations is null
            || item.Recommendations.Count > 4)
        {
            return false;
        }

        var records = new List<LogEvidenceRecordRow>(item.Evidence.Count);
        foreach (var evidence in item.Evidence)
        {
            if (!LogEvidenceRecordRow.TryCreate(evidence, out var record) || record is null)
            {
                return false;
            }

            records.Add(record);
        }

        if (item.UnknownReasons.Any(reason => !IsSafeText(reason, 256)))
        {
            return false;
        }

        var recommendations = new List<LogEvidenceRecommendationRow>(item.Recommendations.Count);
        foreach (var recommendation in item.Recommendations)
        {
            if (!IsSafeText(recommendation.Code, 64)
                || !IsSafeText(recommendation.Label, 128)
                || !IsSafeText(recommendation.Summary, 512))
            {
                return false;
            }

            recommendations.Add(new(
                recommendation.Code,
                recommendation.Label,
                recommendation.Summary));
        }

        row = new(
            item.ServiceId,
            item.ServiceDisplayName,
            item.Category,
            item.Severity,
            item.Confidence,
            item.Summary,
            records,
            item.UnknownReasons.ToArray(),
            recommendations);
        return true;
    }

    private static bool IsSafeText(string? value, int maximumLength)
    {
        if (value is null)
        {
            return false;
        }

        return value.Length <= maximumLength
            && !value.Any(char.IsControl)
            && !value.Contains("connectionstring", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            && !value.Contains(":" + (char)92, StringComparison.Ordinal);
    }
}
