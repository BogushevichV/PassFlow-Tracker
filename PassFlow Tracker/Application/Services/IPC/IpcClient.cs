using PassFlow_Tracker.Domain.Models.IPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace PassFlow_Tracker.Application.Services.IPC
{
    public class IpcClient
    {
        public async Task<IpcResponse> SendAsync(IpcRequest request)
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 5000);

            using var stream = client.GetStream();

            var json = JsonSerializer.Serialize(request);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await stream.WriteAsync(bytes);

            byte[] buffer = new byte[4096];
            int read = await stream.ReadAsync(buffer);

            var responseJson = Encoding.UTF8.GetString(buffer, 0, read);

            return JsonSerializer.Deserialize<IpcResponse>(responseJson)!;
        }
    }
}
