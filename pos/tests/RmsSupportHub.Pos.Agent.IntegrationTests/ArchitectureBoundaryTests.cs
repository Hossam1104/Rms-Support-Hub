using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.Packages;
using RmsSupportHub.Pos.Agent.Repair;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Packages;
using RmsSupportHub.Pos.Infrastructure.Windows;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly string[] ForbiddenAgentRuntimeStrings =
    [
        "AdminPrivilegeManager",
        "ConfigurationService",
        "CryptoService",
        "AgentLegacyConfigurationService",
        "POS_ADMIN_SKIP_ELEVATION",
        "UseStaticFiles",
        "UseDefaultFiles",
        "MapFallbackToFile",
        "AllowAnyOrigin",
        "DisableLoopbackCheck",
        "AngularWorkspaceDir",
        "npm ci",
        "npm run build"
    ];

    [Fact]
    public void AgentRuntimeAndProjectContainNoLegacySpaOrPrivilegeBoundary()
    {
        var root = FindRepoRoot();
        var agentDirectory = Path.Combine(root, "pos", "src", "RmsSupportHub.Pos.Agent");
        var contents = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(agentDirectory, "*", SearchOption.AllDirectories)
                .Where(path => !path.Contains("bin", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("obj", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        foreach (var forbidden in ForbiddenAgentRuntimeStrings)
        {
            Assert.DoesNotContain(forbidden, contents, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AgentProjectIsWebHostWithExactPinnedFoundationPackagesAndNoNodeTargets()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "pos", "src", "RmsSupportHub.Pos.Agent", "RmsSupportHub.Pos.Agent.csproj"));

        Assert.Contains("Sdk=\"Microsoft.NET.Sdk.Web\"", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.AspNetCore.Authentication.Negotiate\" Version=\"10.0.10\"", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.AspNetCore.OpenApi\" Version=\"10.0.10\"", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Extensions.ApiDescription.Server\" Version=\"10.0.10\"", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.OpenApi\" Version=\"2.7.5\"", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Extensions.Hosting.WindowsServices\" Version=\"10.0.10\"", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Angular", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npm", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wwwroot", project, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneralBackendAndRetainedWinUiDoNotReferenceAgentProjects()
    {
        var root = FindRepoRoot();
        var backendFiles = Directory.EnumerateFiles(Path.Combine(root, "backend"), "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains("bin", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("obj", StringComparison.OrdinalIgnoreCase));

        foreach (var file in backendFiles)
        {
            var contents = File.ReadAllText(file);
            Assert.DoesNotContain("RmsSupportHub.Pos.Agent", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("RmsSupportHub.Pos.Infrastructure", contents, StringComparison.Ordinal);
        }

        var winUiFiles = Directory.EnumerateFiles(Path.Combine(root, "pos", "src", "PosAdminTool.WinUI"), "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains("bin", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("obj", StringComparison.OrdinalIgnoreCase));
        foreach (var file in winUiFiles)
        {
            Assert.DoesNotContain("RmsSupportHub.Pos.Agent", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AgentDoesNotContainAStaticWebRoot()
    {
        var root = FindRepoRoot();
        var agentDirectory = Path.Combine(root, "pos", "src", "RmsSupportHub.Pos.Agent");

        Assert.False(Directory.Exists(Path.Combine(agentDirectory, "wwwroot")));
    }

    [Fact]
    public void MetadataOnlyOpenApiCompositionCannotResolveTrustOrPackageLifecycleServices()
    {
        var services = new ServiceCollection();
        AgentOpenApiHost.AddMetadataOnlyLifecycleGuards(services);
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<AgentMachineTrustConfiguration>());
        Assert.Null(provider.GetService<IAgentPackageLifecycle>());
        Assert.Null(provider.GetService<IAgentPackageSignatureVerifier>());
        Assert.Null(provider.GetService<IAgentServiceLifecycleController>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<AgentPackageService>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<RepairService>());
    }

    // L-2: obsolete-key rejection (Program.cs) is only reachable from normal composition. The
    // metadata-only OpenAPI host has its own isolated registration path that structurally never
    // consumes IConfiguration, so a caller supplying either obsolete key -- present with a value,
    // or present but empty -- cannot influence, and is never even read by, the metadata-only graph.
    [Theory]
    [InlineData("PosAgent:ReleaseChannel", "Testing")]
    [InlineData("PosAgent:ReleaseChannel", "")]
    [InlineData("PosAgent:TrustConfigurationPath", "C:\\anywhere\\package-trust.json")]
    [InlineData("PosAgent:TrustConfigurationPath", "")]
    public void MetadataOnlyOpenApiCompositionIgnoresObsoleteTrustKeysRegardlessOfValue(string obsoleteKey, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>(obsoleteKey, value)])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        AgentOpenApiHost.AddMetadataOnlyLifecycleGuards(services);
        using var provider = services.BuildServiceProvider();

        // Building/resolving the metadata-only graph must not throw regardless of the obsolete
        // key's presence or value, and it must still expose no trust/lifecycle authority.
        Assert.Null(provider.GetService<AgentMachineTrustConfiguration>());
        Assert.Null(provider.GetService<IAgentPackageLifecycle>());
        Assert.Null(provider.GetService<IAgentPackageSignatureVerifier>());
        Assert.Null(provider.GetService<IAgentServiceLifecycleController>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<AgentPackageService>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<RepairService>());
    }

    [Fact]
    public void MetadataOnlyLifecycleGuardRegistrationNeverAcceptsConfiguration()
    {
        // Structural proof: the metadata-only registration entry point has no IConfiguration
        // parameter at all, so it cannot read PosAgent:ReleaseChannel/TrustConfigurationPath (or
        // any other key) even if a caller wanted it to.
        var method = typeof(AgentOpenApiHost).GetMethod(nameof(AgentOpenApiHost.AddMetadataOnlyLifecycleGuards));
        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.Single(parameters);
        Assert.DoesNotContain(parameters, parameter => typeof(IConfiguration).IsAssignableFrom(parameter.ParameterType));
    }

    [Fact]
    public void MetadataOnlyOpenApiBranchInProgramNeverContainsObsoleteTrustKeyRejection()
    {
        // The obsolete-key rejection loop must remain exclusive to the normal (!isMetadataOnlyOpenApi)
        // composition branch. Locate the metadata-only ("else") branch text in Program.cs and prove
        // the obsolete keys and their rejection message never appear inside it.
        var root = FindRepoRoot();
        var program = File.ReadAllText(Path.Combine(root, "pos", "src", "RmsSupportHub.Pos.Agent", "Program.cs"));

        var rejectionMessageIndex = program.IndexOf("is obsolete and rejected", StringComparison.Ordinal);
        Assert.True(rejectionMessageIndex >= 0, "Expected to find the obsolete-key rejection message in Program.cs.");

        var elseIndex = program.IndexOf("else", rejectionMessageIndex, StringComparison.Ordinal);
        Assert.True(elseIndex >= 0, "Expected a metadata-only 'else' branch after the obsolete-key rejection check.");

        var branchEndIndex = program.IndexOf("var app = builder.Build();", elseIndex, StringComparison.Ordinal);
        Assert.True(branchEndIndex >= 0, "Expected the composition branches to end before builder.Build().");

        var metadataOnlyBranch = program.Substring(elseIndex, branchEndIndex - elseIndex);

        Assert.DoesNotContain("PosAgent:ReleaseChannel", metadataOnlyBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("PosAgent:TrustConfigurationPath", metadataOnlyBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("is obsolete and rejected", metadataOnlyBranch, StringComparison.Ordinal);
        Assert.Contains("AddMetadataOnlyLifecycleGuards", metadataOnlyBranch, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentProgramDoesNotFabricateSyntheticMachineTrust()
    {
        var root = FindRepoRoot();
        var program = File.ReadAllText(Path.Combine(root, "pos", "src", "RmsSupportHub.Pos.Agent", "Program.cs"));

        Assert.DoesNotContain("new AgentMachineTrustConfiguration", program, StringComparison.Ordinal);
        Assert.Contains("AddMetadataOnlyLifecycleGuards", program, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "pos", "RmsSupportHub.Pos.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
