using System.Windows.Input;

namespace RmsSupportHub.Pos.Desktop.Wpf.ViewModels;

public sealed class AsyncParameterCommand<T>(Func<T?, Task> execute, Func<T?, bool> canExecute) : ICommand
{
    private bool isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !isExecuting && canExecute(Convert(parameter));

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await execute(Convert(parameter)).ConfigureAwait(true);
        }
        finally
        {
            isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Convert(object? parameter) => parameter is T value ? value : default;
}
