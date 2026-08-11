namespace RmsSupportHub.Pos.Contracts.V1.Common;

/// <summary>
/// A single piece of polled status evidence (a services node, local SQL, the main server, ...).
/// Always carries its own freshness and UTC last-checked time rather than a bare boolean, per plan
/// section 7.3 ("Last checked 14:32:08", never a bare "Online").
/// </summary>
public sealed record EvidenceDto(
    FreshnessState Freshness,
    DateTimeOffset? LastCheckedUtc,
    string Detail);
