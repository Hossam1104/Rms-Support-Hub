using RmsSupportHub.Pos.Infrastructure.Diagnostics;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class RmsOperationalHealthTests
{
    [Fact]
    public async Task FixedReaderReturnsAggregateHealthAndNeverNamesSensitiveFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "rms-fixed-health", Guid.NewGuid().ToString("N"));
        try
        {
            var attachments = Path.Combine(root, "attachments");
            var setup = Path.Combine(root, "setup");
            Directory.CreateDirectory(attachments);
            Directory.CreateDirectory(setup);
            await File.WriteAllTextAsync(Path.Combine(attachments, "patient-identifier.pdf"), "synthetic attachment");
            await File.WriteAllTextAsync(Path.Combine(setup, "ReleaseNumber.txt"), "2026.08");

            var reader = new WindowsRmsFixedHealthReader(
                new RmsFixedHealthOptions
                {
                    SetupRoot = setup,
                    DownloadsRoot = Path.Combine(root, "downloads"),
                    ReleaseRepositoryRoot = Path.Combine(root, "repo"),
                    BranchRoot = Path.Combine(root, "branch"),
                    CashierRoot = Path.Combine(root, "cashier"),
                    BranchLogRoot = Path.Combine(root, "branch-logs"),
                    CashierLogRoot = Path.Combine(root, "cashier-logs"),
                    InsuranceAttachmentRoot = attachments
                },
                TimeProvider.System);

            var health = await reader.ReadInsuranceAttachmentHealthAsync();
            var update = await reader.ReadUpdateHealthAsync();

            Assert.Equal(1, health.AttachmentCount);
            Assert.Equal("2026.08", update.ProductRelease);
            Assert.DoesNotContain("patient-identifier.pdf", health.Root.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("patient-identifier.pdf", update.Detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task FixedReaderMarksReparseRootUnavailable()
    {
        var root = Path.Combine(Path.GetTempPath(), "rms-fixed-health", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var reader = new WindowsRmsFixedHealthReader(
                new RmsFixedHealthOptions
                {
                    SetupRoot = Path.Combine(root, "missing-setup"),
                    DownloadsRoot = Path.Combine(root, "missing-downloads"),
                    ReleaseRepositoryRoot = Path.Combine(root, "missing-repo"),
                    BranchRoot = Path.Combine(root, "missing-branch"),
                    CashierRoot = Path.Combine(root, "missing-cashier"),
                    BranchLogRoot = Path.Combine(root, "missing-branch-logs"),
                    CashierLogRoot = Path.Combine(root, "missing-cashier-logs"),
                    InsuranceAttachmentRoot = root
                },
                TimeProvider.System);

            var roots = await reader.ReadRootsAsync();

            Assert.Contains(roots, item => item.RootId == "rms-setup" && item.State == RmsSupportHub.Pos.Domain.Models.RmsFixedRootState.Missing);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
