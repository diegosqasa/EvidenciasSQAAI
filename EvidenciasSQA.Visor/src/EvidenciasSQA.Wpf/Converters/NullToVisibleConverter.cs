using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EvidenciasSQA.Wpf.Converters;

/// <summary>
/// null → Visible, cualquier valor → Collapsed. Para el empty state del visor
/// (spec especificacion-visor-estado-vacio.md §1.1): el placeholder "Sin evidencias"
/// solo se muestra cuando no hay imagen cargada (CurrentImage == null).
/// </summary>
public sealed class NullToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}