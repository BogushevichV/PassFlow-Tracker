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
    public class PeakHourChart
    {
        public int Hour { get; set; }
        public long Flow { get; set; }
        public bool IsPeak { get; set; }

        public PeakHourChart() { }
        public PeakHourChart(int hour, long flow, bool isPeak)
        {
            Hour = hour;
            Flow = flow;
            IsPeak = isPeak;
        }
    }
    // Маршрут для выпадающего списка
    public class RouteItem
    {
        public string UnitName { get; set; } = "";
        public string StartPoint { get; set; } = "";
        public string EndPoint { get; set; } = "";
        public string DisplayName => $"{UnitName}: {StartPoint} → {EndPoint}";

        public RouteItem() { }
        public RouteItem(string unitName, string startPoint, string endPoint)
        {
            UnitName = unitName;
            StartPoint = startPoint;
            EndPoint = endPoint;
        }
    }
    public record StopLoad(string Name, long Load);
    public record LowTrip(int Id, DateTime Time, int Count, string Unit);
    public record TripStopRow(
        int Id,
        int StopNumber,
        string StopName,
        string Period,
        int Entered,
        int Exited,
        int Transported);

    // Режим агрегации для топ-остановок
    public enum TopStopsMode { PerRecord, PerDay, AllTime }

    // Расширенная запись для топ-остановок с датой/периодом
    public record TopStopRow(
        int Id,
        int StopNumber,
        string StopName,
        string Period,
        string RouteName,
        int Entered,
        int Exited,
        int Transported);

    public record DailyRecordRow(
        int Id,
        string UnitName,
        string RecordDate,
        int Entered,
        int Exited,
        int Transported);

    public record RoundRow(
        int Id,
        string UnitName,
        string StartPoint,
        string EndPoint,
        string TimeFrom,
        string TimeTo,
        int Entered,
        int Exited,
        int Transported);

    public record TripRow(
        int Id,
        string UnitName,
        string StartPoint,
        string EndPoint,
        string TimeFrom,
        string TimeTo,
        int Entered,
        int Exited,
        int Transported);

    public record VehicleModelInfo(
        int Id,
        string Name,
        int Seats,
        int Capacity,
        string? Description
    );

    public record VehicleInfo(
        int Id,
        string UnitName,
        int ModelId,
        string ModelName,
        int Seats,         
        int Capacity,      
        string? Description
    );

    public record RouteInfo(
        string RouteNumber,
        string StartPoint,
        string EndPoint
    );

    public record RouteStopInfo(
        int StopNumber,
        string StopName,
        int TotalEntered,
        int TotalExited,
        double AvgTransported
    );

    public enum SchemeLevel { RouteAllTime, Day, Trip }

    public record RouteSchemeData(
        int StopNumber,
        string StopName,
        int Entered,
        int Exited,
        int Transported,
        double FillPercent
    );

    public record DaySummary(
        string Date,
        int TripCount,
        List<DayVehicleInfo> Vehicles,
        List<RouteSchemeData> Stops  
    );

    public record DayVehicleInfo(
        int VehicleId,
        string VehicleName,
        string ModelName,
        int Seats,
        int Capacity,
        int TripCount
    );

    public record TripDetailSummary(
        int TripId,
        string TimeFrom,       
        string TimeTo,        
        string VehicleName,     
        string ModelName,      
        int Capacity,
        int Seats,
        List<TripStopDetail> Stops
    );

    public record TripStopDetail(
        int StopNumber,
        string StopName,
        string TimeFrom,        
        string TimeTo,         
        int Entered,
        int Exited,
        int Transported,
        double FillPercent    
    );

    public record TripSummary(int TripId, string TimeFrom, string VehicleName, string ModelName, int Capacity);

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

    public record TripStopUpdateDto(int Id, int StopNumber, string StopName,
    string TimeFrom, string TimeTo, int Entered, int Exited, int Transported);

    public record TripUpdateDto(int Id, string UnitName, string StartPoint, string EndPoint,
        string TimeFrom, string TimeTo, int Entered, int Exited, int Transported);

    public record RoundUpdateDto(int Id, string UnitName, string StartPoint, string EndPoint,
        string TimeFrom, string TimeTo, int Entered, int Exited, int Transported);

    public record DailyRecordUpdateDto(int Id, string UnitName, string RecordDate,
        int Entered, int Exited, int Transported);
}
