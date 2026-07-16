using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.UI.Converters
{
    public class BoolToHighlightBackgroundConverter : IValueConverter
    {
        public static readonly BoolToHighlightBackgroundConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool highlighted && highlighted)
                return new SolidColorBrush(Color.Parse("#EFF6FF"));
            return new SolidColorBrush(Colors.Transparent);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToHighlightForegroundConverter : IValueConverter
    {
        public static readonly BoolToHighlightForegroundConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool highlighted && highlighted)
                return new SolidColorBrush(Color.Parse("#1D4ED8"));
            return new SolidColorBrush(Color.Parse("#1E293B"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
