using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EvidenciasSQA.Wpf.Converters;

/// <summary>null → Collapsed, cualquier valor → Visible. Para badges con orden opcional.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
