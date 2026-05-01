using PassFlow_Tracker.Domain.Models.Communication;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Application.Services.IPC
{
    public class IpcClient
    {
        private const string LogContext = "IpcClient";
        private readonly string _host;
        private readonly int _port;

        public IpcClient(string host = "127.0.0.1", int port = 5000)
        {
            _host = host;
            _port = port;
        }

        public async Task<IpcResponse> SendAsync(IpcRequest request)
        {
            var requestJson = JsonSerializer.Serialize(request);
            AppLogger.Info($"[{LogContext}] Отправка запроса: {request.Command}");

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(_host, _port);
                AppLogger.Info($"[{LogContext}] Подключен к {_host}:{_port}");

                using var stream = client.GetStream();

                var bytes = Encoding.UTF8.GetBytes(requestJson);
                await stream.WriteAsync(bytes);

                byte[] buffer = new byte[4096];
                int read = await stream.ReadAsync(buffer);

                var responseJson = Encoding.UTF8.GetString(buffer, 0, read);
                AppLogger.Info($"[{LogContext}] Получен ответ: {(responseJson.Length > 100 ? responseJson[..100] + "..." : responseJson)}");

                var response = JsonSerializer.Deserialize<IpcResponse>(responseJson)!;

                if (!response.Success)
                {
                    AppLogger.Warning($"[{LogContext}] Запрос '{request.Command}' выполнен с ошибкой: {response.Message}");
                }

                return response;
            }
            catch (SocketException ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка соединения с {_host}:{_port}", ex);
                return new IpcResponse
                {
                    Success = false,
                    Message = $"Ошибка соединения: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка отправки запроса '{request.Command}'", ex);
                return new IpcResponse
                {
                    Success = false,
                    Message = $"IPC error: {ex.Message}"
                };
            }
        }
    }
}
