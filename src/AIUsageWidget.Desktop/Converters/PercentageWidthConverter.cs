using System.Globalization;
using Avalonia.Data.Converters;

namespace AIUsageWidget.App.Converters;

public sealed class PercentageWidthConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not double width || values[1] is not double percentage)
        {
            return 0d;
        }

        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            return 0d;
        }

        var normalized = Math.Clamp(percentage, 0, 100) / 100d;
        return width * normalized;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Avalonia.Data.BindingOperations.DoNothing).ToArray();
}
