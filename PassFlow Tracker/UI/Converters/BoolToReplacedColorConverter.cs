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
    public class BoolToReplacedColorConverter : IValueConverter
    {
        public static readonly BoolToReplacedColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool replaced && replaced)
                return new SolidColorBrush(Color.Parse("#D97706")); 
            return new SolidColorBrush(Color.Parse("#1E293B")); 
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
