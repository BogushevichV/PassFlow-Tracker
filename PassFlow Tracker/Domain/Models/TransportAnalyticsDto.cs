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
    public record TripStopRow(
        int StopNumber,
        string StopName,
        int Entered,
        int Exited,
        int Transported);

    public record DailyRecordRow(
        string UnitName,
        string RecordDate,
        int Entered,
        int Exited,
        int Transported);

    public record RoundRow(
        string UnitName,
        string StartPoint,
        string EndPoint,
        string TimeFrom,
        string TimeTo,
        int Entered,
        int Exited,
        int Transported);

    public record TripRow(
        string UnitName,
        string StartPoint,
        string EndPoint,
        string TimeFrom,
        string TimeTo,
        int Entered,
        int Exited,
        int Transported);
}
