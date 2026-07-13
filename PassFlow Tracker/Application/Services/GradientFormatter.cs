using PassFlow_Tracker.UI.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PassFlow_Tracker.Application.Services
{
    public sealed class GradientSettings
    {
        public bool MinToMax { get; init; } = true;
        public int ColorSchemeIndex { get; init; }
        public int Steps { get; init; } = 5;

        public static readonly (string Light, string Dark)[] Palettes =
        {
            ("#DBEAFE", "#1D4ED8"),
            ("#DCFCE7", "#15803D"),
            ("#FFE4E6", "#BE123C"),
            ("#FEF9C3", "#A16207"),
        };
    }

    public static class GradientFormatter
    {
        public static void Apply(IEnumerable<IGradientFormattable> rows, string propertyName, GradientSettings settings)
        {
            var rowList = rows.ToList();
            if (rowList.Count == 0) return;

            var values = new List<(IGradientFormattable Row, double Value)>();
            foreach (var row in rowList)
            {
                var value = GetNumericValue(row, propertyName);
                if (value.HasValue)
                    values.Add((row, value.Value));
            }

            if (values.Count == 0) return;

            var min = values.Min(v => v.Value);
            var max = values.Max(v => v.Value);
            var palette = GradientSettings.Palettes[
                Math.Clamp(settings.ColorSchemeIndex, 0, GradientSettings.Palettes.Length - 1)];
            var steps = Math.Clamp(settings.Steps, 2, 10);

            foreach (var row in rowList)
                row.SetCellBackground(propertyName, null);

            foreach (var (row, value) in values)
            {
                var color = max == min
                    ? palette.Light
                    : GetStepColor(value, min, max, palette.Light, palette.Dark, steps, settings.MinToMax);
                row.SetCellBackground(propertyName, color);
            }

            foreach (var row in rowList)
                row.NotifyFormattingChanged();
        }

        private static double? GetNumericValue(IGradientFormattable row, string propertyName)
        {
            var prop = row.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return null;

            return prop.GetValue(row) switch
            {
                int i => i,
                long l => l,
                double d => d,
                float f => f,
                _ => null
            };
        }

        private static string GetStepColor(
            double value, double min, double max,
            string light, string dark, int steps, bool minToMax)
        {
            var normalized = (value - min) / (max - min);
            if (!minToMax) normalized = 1 - normalized;

            var stepIndex = (int)Math.Round(normalized * (steps - 1));
            stepIndex = Math.Clamp(stepIndex, 0, steps - 1);
            var t = steps == 1 ? 0.0 : stepIndex / (double)(steps - 1);

            return InterpolateColor(light, dark, t);
        }

        private static string InterpolateColor(string lightHex, string darkHex, double t)
        {
            var light = ParseColor(lightHex);
            var dark = ParseColor(darkHex);

            var r = (byte)(light.R + (dark.R - light.R) * t);
            var g = (byte)(light.G + (dark.G - light.G) * t);
            var b = (byte)(light.B + (dark.B - light.B) * t);

            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static (byte R, byte G, byte B) ParseColor(string hex)
        {
            hex = hex.TrimStart('#');
            return (
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16)
            );
        }
    }
}
