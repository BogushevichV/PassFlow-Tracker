using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Domain.Models
{
    // --- Вложенные модели данных (DTO) для удобства переноса в UI ---
    public record PeakHour(int Hour, long Flow);
    // Данные для гистограммы часов пик — 24 элемента (час 0..23)
    public record PeakHourChart(int Hour, long Flow, bool IsPeak);
    // Маршрут для выпадающего списка
    public record RouteItem(string UnitName, string StartPoint, string EndPoint)
    {
        public string DisplayName => $"{UnitName}: {StartPoint} → {EndPoint}";
    }
    public record StopLoad(string Name, long Load);
    public record LowTrip(int Id, DateTime Time, int Count, string Unit);
    public record TripStopRow(
        int StopNumber,
        string StopName,
        int Entered,
        int Exited,
        int Transported);

    // Режим агрегации для топ-остановок
    public enum TopStopsMode { PerRecord, PerDay, AllTime }

    // Расширенная запись для топ-остановок с датой/периодом
    public record TopStopRow(
        int StopNumber,
        string StopName,
        string Label,       // название + дата/период для отображения
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

    // --- Иерархические DTO для вкладки "Все данные" ---
    public class AllDataStopDto
    {
        public int StopNumber { get; set; }
        public string StopName { get; set; } = "";
        public bool IsDuplicate { get; set; }
        public bool IsSkipped { get; set; }
        public string TimeFrom { get; set; } = "";
        public string TimeTo { get; set; } = "";
        public int Entered { get; set; }
        public int Exited { get; set; }
        public int Transported { get; set; }
    }

    public class AllDataTripDto
    {
        public string StartPoint { get; set; } = "";
        public string EndPoint { get; set; } = "";
        public string TimeFrom { get; set; } = "";
        public string TimeTo { get; set; } = "";
        public int Entered { get; set; }
        public int Exited { get; set; }
        public int Transported { get; set; }
        public List<AllDataStopDto> Stops { get; set; } = new();
    }

    public class AllDataRoundDto
    {
        public string StartPoint { get; set; } = "";
        public string EndPoint { get; set; } = "";
        public string TimeFrom { get; set; } = "";
        public string TimeTo { get; set; } = "";
        public int Entered { get; set; }
        public int Exited { get; set; }
        public int Transported { get; set; }
        public List<AllDataTripDto> Trips { get; set; } = new();
    }

    public class AllDataDayDto
    {
        public string UnitName { get; set; } = "";
        public string RecordDate { get; set; } = "";
        public int Entered { get; set; }
        public int Exited { get; set; }
        public int Transported { get; set; }
        public List<AllDataRoundDto> Rounds { get; set; } = new();
    }
}
