using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using RmsSupportHub.Pos.Domain.Models;

namespace PosAdminTool.WinUI.Services;

public sealed partial class LogHub : ObservableObject
{
    private const int MaxEntries = 1000;

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    [ObservableProperty]
    private string logText = string.Empty;

    public ObservableCollection<string> Entries { get; } = [];

    public void Add(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";

        _dispatcherQueue.TryEnqueue(() =>
        {
            Entries.Add(line);
            if (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
                LogText = string.Join(Environment.NewLine, Entries);
            }
            else
            {
                LogText = string.IsNullOrEmpty(LogText)
                    ? line
                    : string.Concat(LogText, Environment.NewLine, line);
            }
        });
    }

    public void Clear()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            Entries.Clear();
            LogText = string.Empty;
        });
    }

    public void AddResult(string label, OperationResult result)
    {
        Add($"{label}: {result.Status}");
        foreach (var message in result.Messages)
        {
            Add(message);
        }

        foreach (var error in result.Errors)
        {
            Add(error);
        }
    }
}
