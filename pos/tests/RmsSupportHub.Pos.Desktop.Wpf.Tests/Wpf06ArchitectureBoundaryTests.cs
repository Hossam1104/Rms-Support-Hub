namespace RmsSupportHub.Pos.Desktop.Wpf.Tests;

public sealed class Wpf06ArchitectureBoundaryTests
{
    [Fact]
    public void WpfSourceKeepsAgentAndFilesystemBoundariesIntact()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "pos", "src", "RmsSupportHub.Pos.Desktop.Wpf", "RmsSupportHub.Pos.Desktop.Wpf.csproj"));
        Assert.Contains("RmsSupportHub.Pos.LocalIpc", project, StringComparison.Ordinal);
        Assert.DoesNotContain("RmsSupportHub.Pos.Agent", project, StringComparison.Ordinal);
        Assert.DoesNotContain("RmsSupportHub.Pos.Infrastructure", project, StringComparison.Ordinal);

        var sourceRoot = Path.Combine(root, "pos", "src", "RmsSupportHub.Pos.Desktop.Wpf");
        var sourceFiles = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains("bin", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("obj", StringComparison.OrdinalIgnoreCase));
        foreach (var file in sourceFiles)
        {
            var contents = File.ReadAllText(file);
            Assert.DoesNotContain("RmsSupportHub.Pos.Agent", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("RmsSupportHub.Pos.Infrastructure", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.Data.SqlClient", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Net.Http", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpClient", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("Process.Start", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("PowerShell", contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NamedPipeClientStream", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("ServiceController", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("EventLog", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("File.OpenRead", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("Directory.EnumerateFiles", contents, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BackupWorkspaceHasNoRestoreControlOrCallerSelectedSourceBinding()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "pos", "src", "RmsSupportHub.Pos.Desktop.Wpf", "MainWindow.xaml"));

        Assert.Contains("ShowBackupCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ExportBackupCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ExportSupportBundleCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreCommand", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Content=\"Restore", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ServerPath", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("BackupRootPath", xaml, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TASK.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
