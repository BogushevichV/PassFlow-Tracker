using Avalonia.Data.Converters;
using Avalonia.Media;
using PassFlow_Tracker.UI.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PassFlow_Tracker.UI.Converters
{
    public class ColumnBackgroundConverter : IMultiValueConverter
    {
        public static readonly ColumnBackgroundConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 1 || values[0] is not IGradientFormattable row || parameter is not string column)
                return Brushes.Transparent;

            var hex = row.GetCellBackground(column);
            if (string.IsNullOrEmpty(hex))
                return Brushes.Transparent;

            return new SolidColorBrush(Color.Parse(hex));
        }
    }

    public class ColorSchemeBorderConverter : IValueConverter
    {
        public static readonly ColorSchemeBorderConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not int selectedIndex || parameter is not string indexStr || !int.TryParse(indexStr, out var index))
                return new SolidColorBrush(Color.Parse("#E2E8F0"));

            return new SolidColorBrush(Color.Parse(selectedIndex == index ? "#3B82F6" : "#E2E8F0"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ColorSchemeBorderThicknessConverter : IValueConverter
    {
        public static readonly ColorSchemeBorderThicknessConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not int selectedIndex || parameter is not string indexStr || !int.TryParse(indexStr, out var index))
                return new Avalonia.Thickness(1);

            return new Avalonia.Thickness(selectedIndex == index ? 2 : 1);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
