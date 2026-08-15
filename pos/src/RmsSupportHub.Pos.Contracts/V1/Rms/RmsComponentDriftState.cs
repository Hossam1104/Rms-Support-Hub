namespace RmsSupportHub.Pos.Contracts.V1.Rms;

/// <summary>Comparison state between an installed component build and Product Release.</summary>
public enum RmsComponentDriftState
{
    Aligned,
    Drifted,
    Unavailable
}
