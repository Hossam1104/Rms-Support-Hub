using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RmsSupportHub.Pos.Domain.Enums;

namespace PosAdminTool.WinUI.Controls;

public sealed partial class ServiceCard : UserControl
{
    public static readonly DependencyProperty ServiceNameProperty = DependencyProperty.Register(
        nameof(ServiceName), typeof(string), typeof(ServiceCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(ServiceStatus), typeof(ServiceCard), new PropertyMetadata(ServiceStatus.Unknown));

    public static readonly DependencyProperty StartCommandProperty = DependencyProperty.Register(
        nameof(StartCommand), typeof(ICommand), typeof(ServiceCard), new PropertyMetadata(null));

    public static readonly DependencyProperty StopCommandProperty = DependencyProperty.Register(
        nameof(StopCommand), typeof(ICommand), typeof(ServiceCard), new PropertyMetadata(null));

    public static readonly DependencyProperty RestartCommandProperty = DependencyProperty.Register(
        nameof(RestartCommand), typeof(ICommand), typeof(ServiceCard), new PropertyMetadata(null));

    public ServiceCard()
    {
        InitializeComponent();
    }

    public string ServiceName
    {
        get => (string)GetValue(ServiceNameProperty);
        set => SetValue(ServiceNameProperty, value);
    }

    public ServiceStatus Status
    {
        get => (ServiceStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public ICommand? StartCommand
    {
        get => (ICommand?)GetValue(StartCommandProperty);
        set => SetValue(StartCommandProperty, value);
    }

    public ICommand? StopCommand
    {
        get => (ICommand?)GetValue(StopCommandProperty);
        set => SetValue(StopCommandProperty, value);
    }

    public ICommand? RestartCommand
    {
        get => (ICommand?)GetValue(RestartCommandProperty);
        set => SetValue(RestartCommandProperty, value);
    }
}
