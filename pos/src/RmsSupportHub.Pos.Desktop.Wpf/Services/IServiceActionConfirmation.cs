using System.Windows;
using RmsSupportHub.Pos.Contracts.V1.Services;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public interface IServiceActionConfirmation
{
    Task<bool> ConfirmAsync(
        string displayName,
        ServiceActionKind action,
        CancellationToken cancellationToken = default);
}

public sealed class MessageBoxServiceActionConfirmation : IServiceActionConfirmation
{
    public Task<bool> ConfirmAsync(
        string displayName,
        ServiceActionKind action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var isStop = action == ServiceActionKind.Stop;
        var verb = isStop ? "Stop" : "Restart";
        var explanation = isStop
            ? "The RMS service will become unavailable until it is started again."
            : "The service will be temporarily unavailable.";
        var result = MessageBox.Show(
            $"{verb} {displayName}?\n\n{explanation}",
            $"{verb} service",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No,
            MessageBoxOptions.DefaultDesktopOnly);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
