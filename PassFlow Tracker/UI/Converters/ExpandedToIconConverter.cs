using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace PassFlow_Tracker.UI.Converters
{
    public class ExpandedToIconConverter : IValueConverter
    {
        public static readonly ExpandedToIconConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool expanded)
                return expanded ? "▼" : "▶";
            return "▶";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}