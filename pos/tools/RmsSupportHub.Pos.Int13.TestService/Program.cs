using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "RmsSupportHub.Pos.Int13.TestService")
    .ConfigureServices(services => services.AddHostedService<TestingServiceWorker>())
    .Build();
await host.RunAsync();

internal sealed class TestingServiceWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
}
