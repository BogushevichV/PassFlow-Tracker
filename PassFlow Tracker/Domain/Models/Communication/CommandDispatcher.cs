using PassFlow_Tracker.Application.Services;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                    "low_activity" => await GetLowTrips(request),
                    "trip_stops" => await GetTripStops(),
                    "rounds" => await GetRounds(),
                    "trips" => await GetTrips(),
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
            AppLogger.Info($"[{LogContext}] Импорт JSON из: {path}");

            await _json.ImportAsync(path);

            AppLogger.Info($"[{LogContext}] JSON импортирован успешно");
            return new IpcResponse { Success = true, Message = "JSON imported" };
        }

        private async Task<IpcResponse> GetPeakHours()
        {
            AppLogger.Info($"[{LogContext}] Получение часов пик");
            var data = await _analytics.GetPeakHoursAsync();
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} часов пик");

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

        private async Task<IpcResponse> GetLowTrips(IpcRequest req)
        {
            int threshold = int.Parse(req.Parameters?["threshold"] ?? "10");
            AppLogger.Info($"[{LogContext}] Получение рейсов с активностью < {threshold}");

            var data = await _analytics.GetLowActivityTripsAsync(threshold);
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} рейсов с низкой активностью");

            return new IpcResponse { Success = true, Data = data };
        }
        private async Task<IpcResponse> GetTripStops()
        {
            AppLogger.Info($"[{LogContext}] Получение списка остановок");
            var data = await _analytics.GetTripStopsAsync();
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} остановок");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetRounds()
        {
            AppLogger.Info($"[{LogContext}] Получение кругов");
            var data = await _analytics.GetRoundsAsync();
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} кругов");

            return new IpcResponse { Success = true, Data = data };
        }

        private async Task<IpcResponse> GetTrips()
        {
            AppLogger.Info($"[{LogContext}] Получение рейсов");
            var data = await _analytics.GetTripsAsync();
            AppLogger.Info($"[{LogContext}] Найдено {data.Count} рейсов");

            return new IpcResponse { Success = true, Data = data };
        }
    }
}