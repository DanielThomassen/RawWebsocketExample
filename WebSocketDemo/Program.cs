// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;

var addr = IPAddress.Parse("127.0.0.1");
var port = 80;

var listener = new TcpListener(addr,port);

listener.Start();
Console.WriteLine($"Server started on {addr}:{port}");
while (true)
{
    using var client = await listener.AcceptTcpClientAsync();
    Console.WriteLine($"Client connected..");
    var clientStream = client.GetStream();

    while (true)
    {
        if (!client.Connected)
        {
            Console.WriteLine("Client disconnected");
            break;
        }
        while (!clientStream.DataAvailable || client.Available < 3)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        Console.WriteLine($"Reading request with {client.Available} bytes");
        var bytes = new byte[client.Available];
        _ = await clientStream.ReadAsync(bytes);

        var data = Encoding.UTF8.GetString(bytes);
        if (await ProcessHandshake(clientStream, data))
        {
            Console.WriteLine("Handshake processed");
            continue;
        }
        //00001111 AND with last 4 bytes to get opcode
        var opCode = bytes[0] & 15;
        if (opCode == 1) // text
        {
            var payload = GetMessagePayloadBytes(bytes);
            var text = Encoding.UTF8.GetString(payload);
            Console.WriteLine("Recieved: " + text);
            var response = CreateMessage($"Hello {text}");
            await clientStream.WriteAsync(response);
        }
        else if (opCode == 8)
        {
            break; // Exit
        }
        else // unsupported
        {
            Console.WriteLine($"Unsupported opcode {opCode}");
        }
    }

}

byte[] CreateMessage(string message)
{
    var payload = Encoding.UTF8.GetBytes(message);
    var lenght = payload.Length + 2;
    if (payload.Length >= 125)
    {
        lenght += 2;
    }

    var response = new byte[lenght];
    response[0] = 129;
    var payLoadStart = 2;
    if (payload.Length > 125)
    {
        response[1] = 126;
        var isLittleEndian = BitConverter.IsLittleEndian;
        var lengthBytes = BitConverter.GetBytes(payload.Length);
        if (isLittleEndian)
        {
            lengthBytes = lengthBytes.Reverse().ToArray();
        }
        response[2] = lengthBytes[^2];
        response[3] = lengthBytes[^1];
        payLoadStart = 4;
    }
    else
    {
        response[1] = (byte)payload.Length;
    }

    var len = payload.Length;
    if (payload.Length > response.Length - payLoadStart)
    {
        len = response.Length - payLoadStart;
    }
    Array.Copy(payload,0,response,payLoadStart,payload.Length);

    return response;
}

byte[] GetMessagePayloadBytes(byte[] bytes)
{
    var payLoadInfo = bytes[1];
    var hasMask = (payLoadInfo & 128) > 0;
    long payloadLength = payLoadInfo & 127;
    var maskStart = 2;
    var payloadStart = 2;
    if (payloadLength == 126)
    {
        var isLittleEndian = BitConverter.IsLittleEndian;
        
        var span = bytes[2..4];
        if (isLittleEndian)
        {
            span = span.Reverse().ToArray();
        }
        payloadLength = BitConverter.ToUInt16(span);
        maskStart = 4;
        payloadStart = 4;
    }
    else if(payloadLength > 126)
    {
        payloadLength = (long)BitConverter.ToUInt64(bytes.AsSpan(2,9));
        maskStart = 10;
        payloadStart = 10;
    }

    var payloadEnd = (int)(payloadStart + payloadLength);
    if (hasMask)
    {
        payloadStart += 4;
        var maskEnd = maskStart + 4;
        var mask = bytes.AsSpan(maskStart, maskEnd);
        payloadEnd = (int)(payloadStart + payloadLength);
        for (var i = payloadStart; i < payloadEnd; i++)
        {
            var index = i - payloadStart;
            var maskIndex = index % 4;
            try
            {
                bytes[i] = (byte)(bytes[i] ^ mask[maskIndex]);
            }
            catch(IndexOutOfRangeException)
            {
                Debugger.Break();
            }
            
        }
    }

    
    return bytes[payloadStart .. payloadEnd];
}

async Task<bool> ProcessHandshake(Stream stream, string data)
{
    if (!MyRegex().IsMatch(data))
    {
        return false;
    }
    const string eol = "\r\n"; // HTTP/1.1 defines the sequence CR LF as the end-of-line marker

    var response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + eol
                             + "Connection: Upgrade" + eol
                             + "Upgrade: websocket" + eol
                             + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                                 System.Security.Cryptography.SHA1.Create().ComputeHash(
                                     Encoding.UTF8.GetBytes(
                                         MyRegex1().Match(data).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                                     )
                                 )
                             ) + eol
                             + eol);

    await stream.WriteAsync(response);
    return true;

}

partial class Program
{
    [System.Text.RegularExpressions.GeneratedRegex("^GET")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();

    [System.Text.RegularExpressions.GeneratedRegex("Sec-WebSocket-Key: (.*)")]
    private static partial System.Text.RegularExpressions.Regex MyRegex1();
}