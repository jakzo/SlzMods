using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MelonLoader;

namespace Sst.SpeedrunTimer {
public class WebsocketServer {
  public TcpListener listener;
  private Dictionary<TcpClient, List<byte>> messageBuffers =
      new Dictionary<TcpClient, List<byte>>();
  private List<TcpClient> connectedClients = new List<TcpClient>();

  public Action<TcpClient> OnConnect;
  public Action<string, TcpClient> OnMessage;

  public async Task Start(int port = 6161, string ip = null) {
    listener =
        new TcpListener(ip != null ? IPAddress.Parse(ip) : IPAddress.Any, port);

    listener.Start();

    while (true) {
      var client = await listener.AcceptTcpClientAsync();
      MelonLogger.Msg("Websocket client connected");
      _ = HandleClient(client);
    }
  }

  private async Task HandleClient(TcpClient client) {

    var stream = client.GetStream();
    messageBuffers[client] = new List<byte>();

    var buffer = new byte[1024];

    try {
      while (client.Connected) {
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        if (bytesRead == 0)
          break;

        var bytes = new byte[bytesRead];
        Array.Copy(buffer, bytes, bytesRead);

        if (IsClientHandshake(bytes)) {
          _ = RespondToClientHandshake(bytes, client, stream);
        } else {
          var response = ReadMessage(bytes, client);
          if (response != null) {
            var text = Encoding.UTF8.GetString(response);
            if (OnMessage != null)
              OnMessage(text, client);
            messageBuffers[client].Clear();
          }
        }
      }
    } catch (Exception ex) {
      MelonLogger.Warning("Error with websocket: {0}", ex);
    } finally {
      client.Dispose();
      connectedClients.Remove(client);
      MelonLogger.Msg("Websocket disconnected");
    }
  }

  private bool IsClientHandshake(byte[] request) =>
      Regex.IsMatch(Encoding.UTF8.GetString(request, 0, 4), "^GET\\s",
                    RegexOptions.IgnoreCase);

  private async Task RespondToClientHandshake(byte[] request, TcpClient client,
                                              NetworkStream stream) {
    var requestString = Encoding.UTF8.GetString(request);
    var response = Encoding.UTF8.GetBytes(string.Join("\r\n", new[] {
      "HTTP/1.1 101 Switching Protocols",
      "Connection: Upgrade",
      "Upgrade: websocket",
      $"Sec-WebSocket-Accept: {GenerateWebsocketAcceptToken(requestString)}",
      "",
      "",
    }));
    await stream.WriteAsync(response, 0, response.Length);

    if (connectedClients.Contains(client))
      return;
    connectedClients.Add(client);
    OnConnect(client);
  }

  private string GenerateWebsocketAcceptToken(string request) {
    var swk = Regex
                  .Match(request, "\\n\\s*Sec-WebSocket-Key\\s*:(.*)",
                         RegexOptions.IgnoreCase)
                  .Groups[1]
                  .Value.Trim();
    var swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    var swkaSha1 = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
    return Convert.ToBase64String(swkaSha1);
  }

  private byte[] ReadMessage(byte[] request, TcpClient client) {
    var fin = (request[0] & 0b10000000) != 0;
    var mask = (request[1] & 0b10000000) != 0;
    if (!mask)
      return null;
    // var opcode = request[0] & 0b00001111; // expecting 1 - text message
    var offset = 2;
    var msglen = (ulong)(request[1] & 0b01111111);

    if (msglen == 126) {
      msglen = BitConverter.ToUInt16(new byte[] { request[3], request[2] }, 0);
      offset = 4;
    } else if (msglen == 127) {
      msglen = BitConverter.ToUInt64(
          new byte[] { request[9], request[8], request[7], request[6],
                       request[5], request[4], request[3], request[2] },
          0);
      offset = 10;
    }

    var decoded = new byte[msglen];
    var masksIdx = offset;
    offset += 4;

    for (ulong i = 0; i < msglen; i++)
      decoded[i] = (byte)(request[offset + (int)i] ^
                          request[masksIdx + ((int)i & 0b11)]);

    messageBuffers[client].AddRange(decoded);

    if (!fin)
      return null;
    return messageBuffers[client].ToArray();
  }

  public void Send(byte[] data) {
    foreach (var client in connectedClients.ToArray()) {
      if (client.Connected) {
        _ = Send(data, client);
      } else {
        connectedClients.Remove(client);
      }
    }
  }

  public void Send(string data) {
    foreach (var client in connectedClients.ToArray()) {
      if (client.Connected) {
        _ = Send(data, client);
      } else {
        connectedClients.Remove(client);
      }
    }
  }

  public async Task Send(byte[] data, TcpClient client) {
    var stream = client.GetStream();
    var framedData = CreateMessageFrame(data, false);
    await stream.WriteAsync(framedData, 0, framedData.Length);
  }
  public async Task Send(string data, TcpClient client) {
    var stream = client.GetStream();
    var framedData = CreateMessageFrame(Encoding.UTF8.GetBytes(data), true);
    await stream.WriteAsync(framedData, 0, framedData.Length);
  }

  private byte[] CreateMessageFrame(byte[] data, bool isString) {
    byte[] frame;

    if (data.Length < 126) {
      frame = new byte[data.Length + 2];
      frame[1] = (byte)data.Length;
    } else if (data.Length <= ushort.MaxValue) {
      frame = new byte[data.Length + 4];
      frame[1] = 126;
      var lengthBytes = BitConverter.GetBytes((ushort)data.Length);
      frame[2] = lengthBytes[1];
      frame[3] = lengthBytes[0];
    } else {
      frame = new byte[data.Length + 10];
      frame[1] = 127;
      var lengthBytes = BitConverter.GetBytes((ulong)data.Length);
      for (var i = 0; i < 8; i++) {
        frame[9 - i] = lengthBytes[i];
      }
    }

    frame[0] = (byte)(isString ? 0x81 : 0x82);

    Array.Copy(data, 0, frame, frame.Length - data.Length, data.Length);

    return frame;
  }
}
}
