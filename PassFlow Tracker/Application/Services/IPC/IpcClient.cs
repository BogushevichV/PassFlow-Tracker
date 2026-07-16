using PassFlow_Tracker.Configuration;
using PassFlow_Tracker.Domain.Models.Communication;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using JsonSerializerDefaults = PassFlow_Tracker.Infrastructure.Serialization.JsonSerializerDefaults;

namespace PassFlow_Tracker.Application.Services.IPC
{
    public class IpcClient
    {
        private const string LogContext = "IpcClient";
        private readonly string _host;
        private readonly int _port;

        public IpcClient() : this(AppConfig.Ipc.Host, AppConfig.Ipc.Port) { }

        public IpcClient(string host, int port)
        {
            _host = host;
            _port = port;

            AppLogger.Info($"[IpcClient] Инициализирован для {host}:{port}");
        }

        public async Task<IpcResponse> SendAsync(IpcRequest request)
        {
            request.AuthToken = AppConfig.Ipc.AuthToken;

            var requestJson = JsonSerializer.Serialize(request, JsonSerializerDefaults.OutputOptions);
            AppLogger.Info($"[{LogContext}] Отправка запроса: {request.Command}");

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(_host, _port);
                AppLogger.Info($"[{LogContext}] Подключен к {_host}:{_port}");

                using var stream = client.GetStream();

                var bytes = Encoding.UTF8.GetBytes(requestJson);

                var lengthBytes = BitConverter.GetBytes(bytes.Length);
                await stream.WriteAsync(lengthBytes, 0, 4);

                await stream.WriteAsync(bytes, 0, bytes.Length);

                var responseLengthBuffer = new byte[4];
                await ReadExactlyAsync(stream, responseLengthBuffer, 0, 4);
                int responseLength = BitConverter.ToInt32(responseLengthBuffer, 0);

                const int MaxResponseSize = 100 * 1024 * 1024; // 100 MB
                if (responseLength <= 0 || responseLength > MaxResponseSize)
                {
                    throw new InvalidOperationException($"Некорректная длина ответа: {responseLength}");
                }

                var responseBuffer = new byte[responseLength];
                await ReadExactlyAsync(stream, responseBuffer, 0, responseLength);

                var responseJson = Encoding.UTF8.GetString(responseBuffer, 0, responseLength);
                AppLogger.Info($"[{LogContext}] Получен ответ: {(responseJson.Length > 100 ? responseJson[..100] + "..." : responseJson)}");

                var response = JsonSerializer.Deserialize<IpcResponse>(responseJson, JsonSerializerDefaults.SafeOptions)!;

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

        private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                    throw new EndOfStreamException("Соединение прервано");
                totalRead += read;
            }
        }
    }
}
