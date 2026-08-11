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
        Assert.Contains("Microsoft.Extensions.Hosting.WindowsServices\" Version=\"10.0.10\"", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Angular", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npm", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenApi", project, StringComparison.OrdinalIgnoreCase);
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
