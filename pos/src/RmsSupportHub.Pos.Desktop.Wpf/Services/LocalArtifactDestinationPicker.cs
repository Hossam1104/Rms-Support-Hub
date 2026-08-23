using Microsoft.Win32;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public interface ILocalArtifactDestinationPicker
{
    Task<string?> PickAsync(string suggestedFileName, string extension, CancellationToken cancellationToken = default);
}

public sealed class SaveFileDialogDestinationPicker : ILocalArtifactDestinationPicker
{
    public Task<string?> PickAsync(
        string suggestedFileName,
        string extension,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = extension,
            Filter = extension.Equals(".bak", StringComparison.OrdinalIgnoreCase)
                ? "RMS database backup (*.bak)|*.bak"
                : "RMS Support Bundle (*.zip)|*.zip",
            AddExtension = true,
            OverwritePrompt = false,
            CheckPathExists = true,
            ValidateNames = true,
            Title = "Save RMS artifact"
        };
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }
}

public interface IOverwriteConfirmation
{
    Task<bool> ConfirmAsync(string displayName, CancellationToken cancellationToken = default);
}

public sealed class MessageBoxOverwriteConfirmation : IOverwriteConfirmation
{
    public Task<bool> ConfirmAsync(string displayName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = System.Windows.MessageBox.Show(
            $"'{displayName}' already exists. Replace it?",
            "Confirm overwrite",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);
        return Task.FromResult(result == System.Windows.MessageBoxResult.Yes);
    }
}
