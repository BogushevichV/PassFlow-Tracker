using PassFlow_Tracker.Domain.Models.IPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;


namespace PassFlow_Tracker.Application.Services.IPC
{
    public class IpcServer
    {
        private readonly TcpListener _listener;
        private readonly CommandDispatcher _dispatcher;

        private readonly CancellationTokenSource _cts = new();

        public IpcServer(CommandDispatcher dispatcher, int port = 5000)
        {
            _dispatcher = dispatcher;
            _listener = new TcpListener(IPAddress.Loopback, port);
        }

        public async Task StartAsync()
        {
            _listener.Start();

            while (!_cts.Token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync();

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using var stream = client.GetStream();

            byte[] buffer = new byte[4096];
            int bytes = await stream.ReadAsync(buffer);

            var json = Encoding.UTF8.GetString(buffer, 0, bytes);

            var request = JsonSerializer.Deserialize<IpcRequest>(json);

            var response = await _dispatcher.HandleAsync(request);

            var responseJson = JsonSerializer.Serialize(response);
            var responseBytes = Encoding.UTF8.GetBytes(responseJson);

            await stream.WriteAsync(responseBytes);
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
        }
    }
}
