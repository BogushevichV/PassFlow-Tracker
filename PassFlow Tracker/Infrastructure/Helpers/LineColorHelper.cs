using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Infrastructure.Helpers
{
    public static class LineColorHelper
    {
        public static string GetLineColor(double percent)
        {
            double clamped = Math.Clamp(percent, 0, 120);

            return clamped switch
            {
                <= 25 => Interpolate("#94A3B8", "#60A5FA", clamped / 25.0),       
                <= 50 => Interpolate("#60A5FA", "#16A34A", (clamped - 25) / 25.0), 
                <= 75 => Interpolate("#16A34A", "#EAB308", (clamped - 50) / 25.0), 
                <= 100 => Interpolate("#EAB308", "#F97316", (clamped - 75) / 25.0), 
                _ => Interpolate("#F97316", "#DC2626", (clamped - 100) / 20.0) 
            };
        }

        private static string Interpolate(string fromHex, string toHex, double t)
        {
            t = Math.Clamp(t, 0, 1);

            var (r1, g1, b1) = ParseHex(fromHex);
            var (r2, g2, b2) = ParseHex(toHex);

            int r = (int)(r1 + (r2 - r1) * t);
            int g = (int)(g1 + (g2 - g1) * t);
            int b = (int)(b1 + (b2 - b1) * t);

            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static (int R, int G, int B) ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            return (
                Convert.ToInt32(hex[..2], 16),
                Convert.ToInt32(hex[2..4], 16),
                Convert.ToInt32(hex[4..6], 16)
            );
        }
    }
}
