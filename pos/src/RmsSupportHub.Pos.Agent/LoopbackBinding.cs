using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using RmsSupportHub.Pos.Agent.Security;

namespace RmsSupportHub.Pos.Agent;

/// <summary>
/// Configures the only production listener. The Agent has no HTTP redirect listener, LAN fallback,
/// dynamic port, or configuration path that can widen this binding.
/// </summary>
public static class LoopbackBinding
{
    public const int Port = AgentHostConstants.Port;

    public static void Configure(KestrelServerOptions options, IConfiguration configuration, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        options.AddServerHeader = false;
        options.ListenLocalhost(Port, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
            listenOptions.UseHttps(httpsOptions =>
            {
                // WebApplicationFactory uses TestServer and never creates this Kestrel listener.
                // If a real listener is started in any environment, certificate resolution remains
                // mandatory; IntegrationTest is not a certificate-validation bypass.
                var certificate = AgentCertificateSelector.FindFromLocalMachine(
                    configuration.GetSection(AgentSecurityOptions.SectionName).Get<AgentSecurityOptions>()?.CertificateThumbprint,
                    DateTimeOffset.UtcNow);

                if (certificate is null)
                {
                    throw new InvalidOperationException(
                        "The Agent requires a valid LocalMachine certificate with the canonical DNS SAN.");
                }

                httpsOptions.ServerCertificate = certificate;
            });
        });
    }
}
