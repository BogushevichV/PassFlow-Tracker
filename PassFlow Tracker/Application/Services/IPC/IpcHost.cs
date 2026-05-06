using PassFlow_Tracker.Configuration;
using PassFlow_Tracker.Domain.Models.Communication;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using JsonSerializerDefaults = PassFlow_Tracker.Infrastructure.Serialization.JsonSerializerDefaults;


namespace PassFlow_Tracker.Application.Services.IPC
{
    public class IpcHost
    {
        private const string LogContext = "IpcHost";

        private readonly TcpListener _listener;
        private readonly CommandDispatcher _dispatcher;

        private readonly SemaphoreSlim _connectionLimit;
        private const int MaxConcurrentConnections = 10;

        private readonly CancellationTokenSource _cts = new();

        public IpcHost(CommandDispatcher dispatcher)
            : this(dispatcher, AppConfig.Ipc.Host, AppConfig.Ipc.Port) { }


        public IpcHost(CommandDispatcher dispatcher, string host, int port)
        {
            _dispatcher = dispatcher;
            _listener = new TcpListener(IPAddress.Parse(host), port);

            _connectionLimit = new SemaphoreSlim(MaxConcurrentConnections);

            AppLogger.Info($"[IpcHost] Инициализирован на {host}:{port}");
        }

        public async Task StartAsync()
        {
            _listener.Start();

            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            AppLogger.Info($"[{LogContext}] Хост запущен на {endpoint.Address}:{endpoint.Port}");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    AppLogger.Info($"[{LogContext}] Новое подключение: {client.Client.RemoteEndPoint}");

                    if (!await _connectionLimit.WaitAsync(TimeSpan.FromSeconds(2)))
                    {
                        AppLogger.Warning($"[{LogContext}] Превышен лимит подключений");
                        client.Dispose();
                        continue;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleClientAsync(client);
                        }
                        finally
                        {
                            _connectionLimit.Release(); 
                        }
                    });
                }
                catch (ObjectDisposedException)
                {
                    AppLogger.Info($"[{LogContext}] Хост остановлен (listener disposed)");
                    break;
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"[{LogContext}] Ошибка при приёме подключения", ex);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

            try
            {
                using var stream = client.GetStream();

                var lengthBuffer = new byte[4];
                await ReadExactlyAsync(stream, lengthBuffer, 0, 4);
                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                const int MaxMessageSize = 10 * 1024 * 1024; // 10 MB
                if (messageLength <= 0 || messageLength > MaxMessageSize)
                {
                    AppLogger.Warning($"[{LogContext}] Некорректная длина сообщения от {clientEndpoint}: {messageLength}");
                    return;
                }

                var buffer = new byte[messageLength];
                await ReadExactlyAsync(stream, buffer, 0, messageLength);

                var json = Encoding.UTF8.GetString(buffer, 0, messageLength);
                AppLogger.Info($"[{LogContext}] Получен запрос от {clientEndpoint}: {json}");

                var request = JsonSerializer.Deserialize<IpcRequest>(json, JsonSerializerDefaults.SafeOptions);

                if (!ValidateToken(request?.AuthToken))
                {
                    AppLogger.Warning($"[{LogContext}] Неверный токен от {clientEndpoint}");
                    var errorResponse = new IpcResponse { Success = false, Message = "Unauthorized" };
                    var errorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorResponse));
                    await stream.WriteAsync(errorBytes);
                    return;
                }

                var response = await _dispatcher.HandleAsync(request!);

                var responseJson = JsonSerializer.Serialize(response, JsonSerializerDefaults.OutputOptions);
                AppLogger.Info($"[{LogContext}] Отправлен ответ для {clientEndpoint}: {(response.Success ? "успех" : response.Message)}");

                var responseBytes = Encoding.UTF8.GetBytes(responseJson);

                var responseLengthBytes = BitConverter.GetBytes(responseBytes.Length);
                await stream.WriteAsync(responseLengthBytes, 0, 4);

                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

                AppLogger.Info($"[{LogContext}] Ответ отправлен для {clientEndpoint}: {(response.Success ? "успех" : response.Message)} ({responseBytes.Length} байт)");
            }
            catch (JsonException ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка десериализации запроса от {clientEndpoint}", ex);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[{LogContext}] Ошибка обработки клиента {clientEndpoint}", ex);
            }
            finally
            {
                client.Dispose();
                AppLogger.Info($"[{LogContext}] Соединение с {clientEndpoint} закрыто");
            }
        }

        private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                    throw new EndOfStreamException();
                totalRead += read;
            }
        }

        private static bool ValidateToken(string? token)
        {
            return token == AppConfig.Ipc.AuthToken;
        }

        public void Stop()
        {
            AppLogger.Info($"[{LogContext}] Остановка Хоста...");
            _cts.Cancel();
            _listener.Stop();
            AppLogger.Info($"[{LogContext}] Хост остановлен");
        }
    }
}
