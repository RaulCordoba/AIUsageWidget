using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
namespace AIUsageWidget.App.Converters;
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase) ? value is not true : value is true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}
