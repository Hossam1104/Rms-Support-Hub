using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using RmsSupportHub.Pos.Domain.Enums;

namespace PosAdminTool.WinUI.Converters;

public sealed class ServiceStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var brushKey = value switch
        {
            ServiceStatus.Running => "SuccessBrush",
            ServiceStatus.Stopped => "WarningBrush",
            ServiceStatus.NotFound => "DangerBrush",
            _ => "InfoBrush"
        };

        return (Brush)Microsoft.UI.Xaml.Application.Current.Resources[brushKey];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
