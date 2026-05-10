using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace PassFlow_Tracker.UI.Converters
{
    /// <summary>Конвертирует HeightRatio (0..1) в высоту столбца в пикселях.</summary>
    public class HeightRatioConverter : IValueConverter
    {
        public static readonly HeightRatioConverter Instance = new();
        private const double MaxBarHeight = 260.0;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double ratio)
                return Math.Max(2.0, ratio * MaxBarHeight);
            return 2.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Цвет столбца: пиковый час — оранжевый, остальные — синий.</summary>
    public class PeakColorConverter : IValueConverter
    {
        public static readonly PeakColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isPeak = value is bool b && b;
            return isPeak ? Color.Parse("#F97316") : Color.Parse("#3B82F6");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Цвет подписи часа: пиковый — оранжевый, остальные — серый.</summary>
    public class PeakTextColorConverter : IValueConverter
    {
        public static readonly PeakTextColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isPeak = value is bool b && b;
            return new SolidColorBrush(isPeak ? Color.Parse("#EA580C") : Color.Parse("#64748B"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>FontWeight для подписи часа: пиковый — Bold, остальные — Normal.</summary>
    public class PeakFontWeightConverter : IValueConverter
    {
        public static readonly PeakFontWeightConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isPeak = value is bool b && b;
            return isPeak ? FontWeight.Bold : FontWeight.Normal;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
