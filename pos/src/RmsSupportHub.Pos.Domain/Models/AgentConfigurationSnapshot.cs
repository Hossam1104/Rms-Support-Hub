namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>Configuration plus secret-presence flags — never the secret values themselves (plan
/// section 5.5). This is the shape every read/write operation in <c>AgentConfigurationUseCase</c>
/// returns.</summary>
public sealed record AgentConfigurationSnapshot(AgentConfiguration Configuration, bool HasSqlPassword, bool HasRdbPassword);
