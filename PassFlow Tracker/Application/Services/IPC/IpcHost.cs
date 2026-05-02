using PassFlow_Tracker.Configuration;
using PassFlow_Tracker.Domain.Models.Communication;
using PassFlow_Tracker.Infrastructure.Logging;
using System;
using System.Collections.Generic;
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
                            _connectionLimit.Release(); // ← освобождаем слот
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

                byte[] buffer = new byte[4096];
                int bytes = await stream.ReadAsync(buffer);

                var json = Encoding.UTF8.GetString(buffer, 0, bytes);
                AppLogger.Info($"[{LogContext}] Получен запрос от {clientEndpoint}: {json}");

                var request = JsonSerializer.Deserialize<IpcRequest>(json, JsonSerializerDefaults.SafeOptions);

                if (!ValidateToken(request?.AuthToken))
                {
                    AppLogger.Warning($"[IpcHost] Неверный токен от {clientEndpoint}");
                    var errorResponse = new IpcResponse { Success = false, Message = "Unauthorized" };
                    var errorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorResponse));
                    await stream.WriteAsync(errorBytes);
                    return;
                }

                var response = await _dispatcher.HandleAsync(request);

                var responseJson = JsonSerializer.Serialize(response, JsonSerializerDefaults.OutputOptions);
                AppLogger.Info($"[{LogContext}] Отправлен ответ для {clientEndpoint}: {(response.Success ? "успех" : response.Message)}");

                var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                await stream.WriteAsync(responseBytes);
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

        private static bool ValidateToken(string? token)
        {
            // В продакшене — хеш или JWT
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
