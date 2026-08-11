using RmsSupportHub.Pos.Application.UseCases;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class ImportFromRmsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncReadsReleaseNumberFromRmsInfo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"pos_admin_import_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var rmsInfoPath = Path.Combine(tempDir, "RMSInfo.json");
            await File.WriteAllTextAsync(
                rmsInfoPath,
                """
                {
                  "ServerName": ".",
                  "UserName": "sa",
                  "Password": "P@ssw0rd",
                  "BranchCode": "P004",
                  "POSNumber": "1",
                  "ReleaseNumber": "2.4.9"
                }
                """);

            var configuration = new FakeConfigurationService(new AppSettings
            {
                RmsInfoPath = rmsInfoPath,
                Release = "N/A"
            });
            var useCase = new ImportFromRmsUseCase(configuration);

            var result = await useCase.ExecuteAsync();

            Assert.True(result.Success);
            Assert.Equal("2.4.9", configuration.SavedSettings?.Release);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsyncReadsClientNameWithoutJsonQuotes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"pos_admin_import_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var rmsInfoPath = Path.Combine(tempDir, "RMSInfo.json");
            var cashierUiPath = Path.Combine(tempDir, "appsettings.json");

            await File.WriteAllTextAsync(
                rmsInfoPath,
                """
                {
                  "ServerName": ".",
                  "BranchCode": "P004"
                }
                """);

            await File.WriteAllTextAsync(
                cashierUiPath,
                """
                {
                  "Settings": {
                    "TheClient": "UPC"
                  }
                }
                """);

            var configuration = new FakeConfigurationService(new AppSettings
            {
                RmsInfoPath = rmsInfoPath,
                CashierUiConfigPath = cashierUiPath,
                ClientName = "N/A"
            });
            var useCase = new ImportFromRmsUseCase(configuration);

            var result = await useCase.ExecuteAsync();

            Assert.True(result.Success);
            Assert.Equal("UPC", configuration.SavedSettings?.ClientName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private sealed class FakeConfigurationService(AppSettings settings) : IConfigurationService
    {
        public AppSettings? SavedSettings { get; private set; }

        public string ConfigFilePath => "test-config.json";

        public string? LastLoadError => null;

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(settings.Clone());
        }

        public Task SaveAsync(AppSettings savedSettings, CancellationToken cancellationToken = default)
        {
            SavedSettings = savedSettings.Clone();
            return Task.CompletedTask;
        }

        public Task<AppSettings> UpdateAsync(Action<AppSettings> modifier, CancellationToken cancellationToken = default)
        {
            modifier(settings);
            return Task.FromResult(settings.Clone());
        }
    }
}
