using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Domain.Models
{
    // --- Вложенные модели данных (DTO) для удобства переноса в UI ---
    public record PeakHour(int Hour, long Flow);
    public record StopLoad(string Name, long Load);
    public record LowTrip(int Id, DateTime Time, int Count, string Unit);
}
