namespace RmsSupportHub.Pos.Contracts.V1.Configuration;

/// <summary>
/// Clearing a secret is a distinct, explicitly authorized operation from replacing one (plan
/// section 5.5) — never a side effect of a blank field on <see cref="ConfigurationUpdateRequestDto"/>.
/// </summary>
public sealed record ClearSecretRequestDto(SecretKind Secret, long ExpectedVersion);
