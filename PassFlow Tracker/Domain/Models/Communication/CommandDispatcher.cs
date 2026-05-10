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
                    "import_json" => await ImportJson(request),
                    "peak_hours" => await GetPeakHours(),
                    "top_stops" => await GetTopStops(request),
                    "top_stops_detailed" => await GetTopStopsDetailed(request),
                    "low_activity" => await GetLowTrips(request),
                    "trip_stops" => await GetTripStops(request),
                    "rounds" => await GetRounds(request),
                    "trips" => await GetTrips(request),
                    "daily_records" => await GetDailyRecords(request),
                    "all_data" => await GetAllData(request),
                    "update_trip_stops" => await UpdateTripStops(request),
                    "update_trips" => await UpdateTrips(request),
                    "update_rounds" => await UpdateRounds(request),
                    "update_daily_records" => await UpdateDailyRecords(request),
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

            // Проверка на пустой путь
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
            if (req.Parameters != null && req.Parameters.TryGetValue("unit", out var unit) && !string.IsNullOrEmpty(unit))
                unitValue = unit;
            AppLogger.Info($"[{LogContext}] Гистограмма часов пик, маршрут={unitValue ?? "все"}");
            var data = await _analytics.GetPeakHoursChartAsync(unitValue);
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRoutes()
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
            AppLogger.Info($"[{LogContext}] Получение списка остановок");
            var data = await _analytics.GetTripStopsAsync(ids);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} остановок");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRounds(IpcRequest req)
        {
            var ids = DeserializeIds(req);
            AppLogger.Info($"[{LogContext}] Получение кругов");
            var data = await _analytics.GetRoundsAsync(ids);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} кругов");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetTrips(IpcRequest req)
        {
            var ids = DeserializeIds(req);
            AppLogger.Info($"[{LogContext}] Получение рейсов");
            var data = await _analytics.GetTripsAsync(ids);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} рейсов");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetDailyRecords(IpcRequest req)
        {
            var ids = DeserializeIds(req);
            AppLogger.Info($"[{LogContext}] Получение дней");
            var data = await _analytics.GetDailyRecordsAsync(ids);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} дней");
            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetAllData(IpcRequest req)
        {
            var ids = DeserializeIds(req);
            AppLogger.Info($"[{LogContext}] Получение всех данных (дерево)");
            var data = await _analytics.GetAllDataAsync(ids);
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
    }
}