using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PosAdminTool.WinUI.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var brushKey = value is true ? "SuccessBrush" : "DangerBrush";
        return (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources[brushKey];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
