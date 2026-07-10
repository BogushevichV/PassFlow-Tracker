using DocumentFormat.OpenXml.Drawing;
using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using JsonSerializerDefaults = PassFlow_Tracker.Infrastructure.Serialization.JsonSerializerDefaults;

namespace PassFlow_Tracker.Domain.Models.Communication
{
    public class CommandDispatcher
    {
        private const string LogContext = "CommandDispatcher";

        private readonly JsonImportService _json;
        private readonly TransportAnalytics _analytics;

        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public CommandDispatcher(JsonImportService json, TransportAnalytics analytics)
        {
            _json = json;
            _analytics = analytics;
        }

        public async Task<IpcResponse> HandleAsync(IpcRequest request)
        {
            AppLogger.Info($"[{LogContext}] Выполнение команды: {request.Command}");
            var startTime = DateTime.Now;

            await _semaphore.WaitAsync();

            try
            {
                var response = request.Command switch
                {
                    "import_json"           => await ImportJson(request),
                    "peak_hours"            => await GetPeakHours(),
                    "peak_hours_chart"      => await GetPeakHoursChart(request),
                    "routes"                => await GetRoutes(request),
                    "top_stops"             => await GetTopStops(request),
                    "top_stops_detailed"    => await GetTopStopsDetailed(request),
                    "low_activity"          => await GetLowTrips(request),
                    "trip_stops"            => await GetTripStops(request),
                    "vehicle_models"        => await GetVehicleModels(),
                    "update_vehicle_model"  => await UpdateVehicleModel(request),
                    "vehicles"              => await GetVehicles(),
                    "update_vehicle"        => await UpdateVehicle(request),
                    "rounds"                => await GetRounds(request),
                    "trips"                 => await GetTrips(request),
                    "daily_records"         => await GetDailyRecords(request),
                    "daily_flow"            => await GetDailyFlow(request),
                    "all_data"              => await GetAllData(request),
                    "update_trip_stops"     => await UpdateTripStops(request),
                    "update_trips"          => await UpdateTrips(request),
                    "update_rounds"         => await UpdateRounds(request),
                    "update_daily_records"  => await UpdateDailyRecords(request),
                    "distinct_routes"       => await GetDistinctRoutes(),
                    "route_scheme_all"      => await GetRouteSchemeAllTime(request),
                    "route_scheme_day"      => await GetRouteSchemeDay(request),
                    "route_scheme_trip"     => await GetRouteSchemeTrip(request),
                    "route_days"            => await GetRouteDays(request),
                    "route_trips"           => await GetRouteTrips(request),
                    "route_vehicles"        => await GetRouteVehicles(request),
                    "route_trips_detailed"  => await GetRouteTripsDetailed(request),
                    _ => new IpcResponse { Success = false, Message = "Unknown command" }
                };

                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Info($"[{LogContext}] Команда '{request.Command}' выполнена за {duration:F0}мс");

                return response;
            }
            catch (Exception ex)
            {
                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                AppLogger.Error($"[{LogContext}] Ошибка выполнения команды '{request.Command}' (за {duration:F0}мс)", ex);

                return new IpcResponse
                {
                    Success = false,
                    Message = $"Ошибка выполнения: {ex.Message}"
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<IpcResponse> ImportJson(IpcRequest req)
        {
            var path = req.Parameters?["path"];

            if (string.IsNullOrEmpty(path))
            {
                AppLogger.Warning($"[{LogContext}] Импорт JSON: путь не указан");
                return new IpcResponse
                {
                    Success = false,
                    Message = "Путь к файлу не указан"
                };
            }

            AppLogger.Info($"[{LogContext}] Импорт JSON из: {path}");

            try
            {
                var importedIds = await _json.ImportAsync(path);

                AppLogger.Info($"[{LogContext}] JSON импортирован успешно. Записей: {importedIds.Count}");

                return new IpcResponse
                {
                    Success = true,
                    Message = $"Импортировано записей: {importedIds.Count}",
                    Data = importedIds
                };
            }
            catch (FileNotFoundException)
            {
                AppLogger.Warning($"[{LogContext}] Файл не найден: {path}");
                return new IpcResponse
                {
                    Success = false,
                    Message = "Файл не найден"
                };
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка импорта JSON", ex);
                return new IpcResponse
                {
                    Success = false,
                    Message = "Внутренняя ошибка при импорте"
                };
            }
        }

        private async Task<IpcResponse> GetPeakHours()
        {
            AppLogger.Info($"[{LogContext}] Получение часов пик");
            var data = await _analytics.GetPeakHoursAsync();
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} часов пик");
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetPeakHoursChart(IpcRequest req)
        {
            string? unitValue = null;
            if (req.Parameters != null && req.Parameters.TryGetValue("unit", out var unit))
                unitValue = string.IsNullOrEmpty(unit) ? null : unit;
            AppLogger.Info($"[{LogContext}] Гистограмма часов пик, маршрут={unitValue ?? "все"}");
            var data = await _analytics.GetPeakHoursChartAsync(unitValue);
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRoutes(IpcRequest req)
        {
            AppLogger.Info($"[{LogContext}] Получение маршрутов");
            var data = await _analytics.GetRoutesAsync();
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetTopStops(IpcRequest req)
        {
            int limit = int.Parse(req.Parameters?["limit"] ?? "10");
            AppLogger.Info($"[{LogContext}] Получение топ-{limit} остановок");

            var data = await _analytics.GetTopStopsAsync(limit);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} остановок");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetTopStopsDetailed(IpcRequest req)
        {
            int limit = int.Parse(req.Parameters?["limit"] ?? "10");
            var modeStr = req.Parameters?["mode"] ?? "PerRecord";
            var mode = Enum.Parse<TopStopsMode>(modeStr);
            var ids = DeserializeIds(req);

            AppLogger.Info($"[{LogContext}] Топ-{limit} остановок, режим={mode}");
            var data = await _analytics.GetTopStopsDetailedAsync(limit, mode, ids);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} записей");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetLowTrips(IpcRequest req)
        {
            int threshold = int.Parse(req.Parameters?["threshold"] ?? "10");
            AppLogger.Info($"[{LogContext}] Получение рейсов с entered < {threshold}");

            var data = await _analytics.GetLowActivityTripsAsync(threshold);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} рейсов с низкой активностью");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetTripStops(IpcRequest req)
        {
            var ids = DeserializeIds(req);
            var (from, to) = ParseDateFilter(DeserializeDateFilter(req));

            AppLogger.Info($"[{LogContext}] Получение списка остановок");
            var data = await _analytics.GetTripStopsAsync(ids, from, to);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} остановок");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetVehicleModels()
        {
            var data = await _analytics.GetVehicleModelsAsync();
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> UpdateVehicleModel(IpcRequest req)
        {
            var id = int.Parse(req.Parameters?["id"]!);
            var seats = int.Parse(req.Parameters?["seats"]!);
            var capacity = int.Parse(req.Parameters?["capacity"]!);
            var desc = req.Parameters?.GetValueOrDefault("description");

            await _analytics.UpdateVehicleModelAsync(id, seats, capacity, desc);
            return new IpcResponse { Success = true, Message = "Модель обновлена" };
        }

        private async Task<IpcResponse> GetVehicles()
        {
            var data = await _analytics.GetVehiclesAsync();
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> UpdateVehicle(IpcRequest req)
        {
            var id = int.Parse(req.Parameters?["id"]!);
            var modelId = int.Parse(req.Parameters?["model_id"]!);
            var desc = req.Parameters?.GetValueOrDefault("description");
            var unitName = req.Parameters?.GetValueOrDefault("unit_name");

            await _analytics.UpdateVehicleAsync(id, modelId, unitName, desc);
            return new IpcResponse { Success = true, Message = "Машина обновлена" };
        }

        private async Task<IpcResponse> GetRounds(IpcRequest req)
        {
            var ids = DeserializeIds(req);
            var (from, to) = ParseDateFilter(DeserializeDateFilter(req));

            AppLogger.Info($"[{LogContext}] Получение кругов");
            var data = await _analytics.GetRoundsAsync(ids, from, to);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} кругов");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetTrips(IpcRequest req)
        {
            var ids = DeserializeIds(req);
            var (from, to) = ParseDateFilter(DeserializeDateFilter(req));

            AppLogger.Info($"[{LogContext}] Получение рейсов");
            var data = await _analytics.GetTripsAsync(ids, from, to);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} рейсов");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetDailyRecords(IpcRequest req)
        {
            var ids = DeserializeIds(req);
            var (from, to) = ParseDateFilter(DeserializeDateFilter(req));

            AppLogger.Info($"[{LogContext}] Получение дней");
            var data = await _analytics.GetDailyRecordsAsync(ids, from, to);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} дней");
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetDailyFlow(IpcRequest req)
        {
            var from = DateOnly.Parse(req.Parameters?["from"]!);
            var to = DateOnly.Parse(req.Parameters?["to"]!);

            AppLogger.Info($"[{LogContext}] Получение пассажиропотока для заданного диапазона ({from} - {to})");
            var data = await _analytics.GetDailyFlowAsync(from, to);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} пассажиропотоков");
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetAllData(IpcRequest req)
        {
            var ids = DeserializeIds(req);
            var (from, to) = ParseDateFilter(DeserializeDateFilter(req));

            AppLogger.Info($"[{LogContext}] Получение всех данных (дерево)");
            var data = await _analytics.GetAllDataAsync(ids, from, to);
            AppLogger.Info($"[{LogContext}] Загружено {data.Count} дней");
            return new IpcResponse { Success = true, Data = data };
        }

        private List<int>? DeserializeIds(IpcRequest req)
        {
            if (req.Parameters == null || !req.Parameters.TryGetValue("ids", out var idsJson))
                return null;
            if (string.IsNullOrEmpty(idsJson)) return null;

            return JsonSerializer.Deserialize<List<int>>(idsJson);
        }

        private static (DateOnly? From, DateOnly? To) ParseDateFilter(DateFilter? filter)
        {
            if (filter == null) return (null, null);

            DateOnly? from = null;
            DateOnly? to = null;

            if (!string.IsNullOrEmpty(filter.From))
                from = DateOnly.Parse(filter.From);

            if (!string.IsNullOrEmpty(filter.To))
                to = DateOnly.Parse(filter.To);

            return (from, to);
        }

        private DateFilter? DeserializeDateFilter(IpcRequest req)
        {
            var filterJson = req.Parameters?.GetValueOrDefault("dateFilter");
            if (string.IsNullOrEmpty(filterJson)) return null;

            return JsonSerializer.Deserialize<DateFilter>(filterJson, JsonSerializerDefaults.SafeOptions);
        }

        public record DateFilter(string From, string To);

        private async Task<IpcResponse> UpdateTripStops(IpcRequest req)
        {
            var json = req.Parameters?["data"];
            if (string.IsNullOrEmpty(json))
                return new IpcResponse { Success = false, Message = "No data" };

            var data = JsonSerializer.Deserialize<List<TripStopUpdateDto>>(json, JsonSerializerDefaults.SafeOptions);
            if (data == null || data.Count == 0)
                return new IpcResponse { Success = false, Message = "Empty data" };

            AppLogger.Info($"[{LogContext}] Обновление остановок: {data.Count} записей");
            await _analytics.UpdateTripStopsAsync(data);
            AppLogger.Info($"[{LogContext}] Остановки обновлены");

            return new IpcResponse { Success = true, Message = $"Обновлено остановок: {data.Count}" };
        }

        private async Task<IpcResponse> UpdateTrips(IpcRequest req)
        {
            var json = req.Parameters?["data"];
            if (string.IsNullOrEmpty(json))
                return new IpcResponse { Success = false, Message = "No data" };

            var data = JsonSerializer.Deserialize<List<TripUpdateDto>>(json, JsonSerializerDefaults.SafeOptions);
            if (data == null || data.Count == 0)
                return new IpcResponse { Success = false, Message = "Empty data" };

            AppLogger.Info($"[{LogContext}] Обновление рейсов: {data.Count} записей");
            await _analytics.UpdateTripsAsync(data);
            AppLogger.Info($"[{LogContext}] Рейсы обновлены");

            return new IpcResponse { Success = true, Message = $"Обновлено рейсов: {data.Count}" };
        }

        private async Task<IpcResponse> UpdateRounds(IpcRequest req)
        {
            var json = req.Parameters?["data"];
            if (string.IsNullOrEmpty(json))
                return new IpcResponse { Success = false, Message = "No data" };

            var data = JsonSerializer.Deserialize<List<RoundUpdateDto>>(json, JsonSerializerDefaults.SafeOptions);
            if (data == null || data.Count == 0)
                return new IpcResponse { Success = false, Message = "Empty data" };

            AppLogger.Info($"[{LogContext}] Обновление кругов: {data.Count} записей");
            await _analytics.UpdateRoundsAsync(data);
            AppLogger.Info($"[{LogContext}] Круги обновлены");

            return new IpcResponse { Success = true, Message = $"Обновлено кругов: {data.Count}" };
        }

        private async Task<IpcResponse> UpdateDailyRecords(IpcRequest req)
        {
            var json = req.Parameters?["data"];
            if (string.IsNullOrEmpty(json))
                return new IpcResponse { Success = false, Message = "No data" };

            var data = JsonSerializer.Deserialize<List<DailyRecordUpdateDto>>(json, JsonSerializerDefaults.SafeOptions);
            if (data == null || data.Count == 0)
                return new IpcResponse { Success = false, Message = "Empty data" };

            AppLogger.Info($"[{LogContext}] Обновление дней: {data.Count} записей");
            await _analytics.UpdateDailyRecordsAsync(data);
            AppLogger.Info($"[{LogContext}] Дни обновлены");

            return new IpcResponse { Success = true, Message = $"Обновлено дней: {data.Count}" };
        }

        private async Task<IpcResponse> GetDistinctRoutes()
        {
            var data = await _analytics.GetDistinctRoutesAsync();
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRouteSchemeAllTime(IpcRequest req)
        {
            var start = req.Parameters?["start"]!;
            var end = req.Parameters?["end"]!;
            int? vehicleId = req.Parameters?.TryGetValue("vehicle_id", out var vid) == true
                ? int.Parse(vid) : null;

            var data = await _analytics.GetRouteSchemeAllTimeAsync(start, end, vehicleId);
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRouteSchemeDay(IpcRequest req)
        {
            var start = req.Parameters?["start"]!;
            var end = req.Parameters?["end"]!;
            var date = DateOnly.Parse(req.Parameters?["date"]!);
            int? vehicleId = req.Parameters?.TryGetValue("vehicle_id", out var vid) == true
                ? int.Parse(vid) : null;

            var data = await _analytics.GetRouteSchemeDayAsync(start, end, date, vehicleId);
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRouteSchemeTrip(IpcRequest req)
        {
            var tripId = int.Parse(req.Parameters?["trip_id"]!);
            int? vehicleId = req.Parameters?.TryGetValue("vehicle_id", out var vid) == true
                ? int.Parse(vid) : null;

            var data = await _analytics.GetRouteSchemeTripAsync(tripId, vehicleId);
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRouteDays(IpcRequest req)
        {
            var start = req.Parameters?["start"]!;
            var end = req.Parameters?["end"]!;
            var data = await _analytics.GetRouteDaysAsync(start, end);
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRouteTrips(IpcRequest req)
        {
            var start = req.Parameters?["start"]!;
            var end = req.Parameters?["end"]!;
            var date = DateOnly.Parse(req.Parameters?["date"]!);
            var data = await _analytics.GetRouteTripsAsync(start, end, date);
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRouteVehicles(IpcRequest req)
        {
            var start = req.Parameters?["start"]!;
            var end = req.Parameters?["end"]!;
            var date = DateOnly.Parse(req.Parameters?["date"]!);
            var data = await _analytics.GetRouteVehiclesAsync(start, end, date);
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRouteTripsDetailed(IpcRequest req)
        {
            var start = req.Parameters?["start"]!;
            var end = req.Parameters?["end"]!;
            var date = DateOnly.Parse(req.Parameters?["date"]!);
            var data = await _analytics.GetRouteTripsDetailedAsync(start, end, date);
            return new IpcResponse { Success = true, Data = data };
        }
    }
}