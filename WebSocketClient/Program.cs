// See https://aka.ms/new-console-template for more information

using System.Net.WebSockets;
using System.Text;

var sock = new ClientWebSocket();
await sock.ConnectAsync(new Uri("ws://127.0.0.1"),CancellationToken.None);
Console.WriteLine("Connected");
var buffer = new byte[1024];
var data = new List<byte>();
while (true)
{
    Console.WriteLine("Input value");
    var line = Console.ReadLine();
    if (string.IsNullOrEmpty(line))
    {
        continue;
    }
    var bytes = Encoding.UTF8.GetBytes(line);
    await sock.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    
    var result = await ReadData();
    while (!result.EndOfMessage)
    {
        result = await ReadData();
    }

    var response = Encoding.UTF8.GetString(data.ToArray());
    Console.WriteLine(response);
}

async Task<WebSocketReceiveResult> ReadData()
{
    Array.Clear(buffer);
    var result = await sock.ReceiveAsync(buffer, CancellationToken.None);
    data.AddRange(buffer);
    return result;
} 