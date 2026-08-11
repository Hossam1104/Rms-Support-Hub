using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

public interface IConfigurationService
{
    string ConfigFilePath { get; }

    string? LastLoadError { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task<AppSettings> UpdateAsync(Action<AppSettings> modifier, CancellationToken cancellationToken = default);
}
