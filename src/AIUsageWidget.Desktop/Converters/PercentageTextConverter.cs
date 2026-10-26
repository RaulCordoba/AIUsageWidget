using System.Globalization;
using Avalonia.Data.Converters;

namespace AIUsageWidget.App.Converters;

public sealed class PercentageTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double number)
        {
            return $"{number:0.#} %";
        }

        return "N/D";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
