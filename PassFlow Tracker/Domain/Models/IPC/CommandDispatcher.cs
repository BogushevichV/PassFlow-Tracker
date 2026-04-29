using PassFlow_Tracker.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Domain.Models.IPC
{
    public class CommandDispatcher
    {
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
            await _semaphore.WaitAsync(); 

            try
            {
                return request.Command switch
                {
                    "import_json" => await ImportJson(request),
                    "peak_hours" => await GetPeakHours(),
                    "top_stops" => await GetTopStops(request),
                    "low_activity" => await GetLowTrips(request),
                    _ => new IpcResponse { Success = false, Message = "Unknown command" }
                };
            }
            catch (Exception ex)
            {
                return new IpcResponse
                {
                    Success = false,
                    Message = ex.Message
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

            await _json.ImportAsync(path);

            return new IpcResponse
            {
                Success = true,
                Message = "JSON imported"
            };
        }

        private async Task<IpcResponse> GetPeakHours()
        {
            var data = await _analytics.GetPeakHoursAsync();

            return new IpcResponse
            {
                Success = true,
                Data = data
            };
        }

        private async Task<IpcResponse> GetTopStops(IpcRequest req)
        {
            int limit = int.Parse(req.Parameters?["limit"] ?? "10");

            var data = await _analytics.GetTopStopsAsync(limit);

            return new IpcResponse
            {
                Success = true,
                Data = data
            };
        }

        private async Task<IpcResponse> GetLowTrips(IpcRequest req)
        {
            int threshold = int.Parse(req.Parameters?["threshold"] ?? "10");

            var data = await _analytics.GetLowActivityTripsAsync(threshold);

            return new IpcResponse
            {
                Success = true,
                Data = data
            };
        }
    }
}