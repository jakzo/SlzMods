using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MelonLoader;

namespace Sst.SpeedrunTimer {
public class WebsocketServer {
  public class Client {
    public TcpClient TcpClient;
    public List<byte> MessageBuffer = new List<byte>();
    public BlockingCollection<byte[]> SendQueue =
        new BlockingCollection<byte[]>();

    public void Send(string data) { Send(Encoding.UTF8.GetBytes(data), true); }
    public void Send(byte[] data, bool isString = false) {
      SendQueue.Add(CreateMessageFrame(data, isString));
    }
  }

  public TcpListener listener;
  private List<Client> connectedClients = new List<Client>();

  public Action<Client> OnConnect;
  public Action<string, Client> OnMessage;

  // Websocket is not supported in the Mono packaged with MelonLoader 5
  // We also cannot use the async methods on Quest, hence sync + threads
  public void Start(int port = 6161, string ip = null) {
    listener =
        new TcpListener(ip != null ? IPAddress.Parse(ip) : IPAddress.Any, port);
    listener.Start();
    Task.Run(ListenForConnections);
  }

  public void Send(string data) { Send(Encoding.UTF8.GetBytes(data), true); }
  public void Send(byte[] data, bool isString = false) {
    var framedData = CreateMessageFrame(data, isString);
    foreach (var client in connectedClients.ToArray()) {
      if (client.TcpClient.Connected) {
        client.SendQueue.Add(framedData);
      } else {
        connectedClients.Remove(client);
      }
    }
  }

  private void ListenForConnections() {
    while (true) {
      try {
        var tcpClient = listener.AcceptTcpClient();
        MelonLogger.Msg("Websocket client connected");
        var client = new Client() { TcpClient = tcpClient };
        connectedClients.Add(client);
        Task.Run(() => HandleClient(client));
        var sendThread = new Thread(() => ListenToSendQueue(client)) {
          IsBackground = true,
        };
        sendThread.Start();
      } catch (Exception ex) {
        MelonLogger.Error("Error listening for Websocket connections:");
        MelonLogger.Error(ex);
        Thread.Sleep(5000); // avoid spam on persistent errors
      }
    }
  }

  private void HandleClient(Client client) {
    var buffer = new byte[1024];
    try {
      var stream = client.TcpClient.GetStream();
      while (client.TcpClient.Connected) {
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        if (bytesRead == 0)
          break;

        var bytes = new byte[bytesRead];
        Array.Copy(buffer, bytes, bytesRead);

        if (IsClientHandshake(bytes)) {
          RespondToClientHandshake(bytes, client);
        } else {
          var response = ReadMessage(bytes, client);
          if (response != null) {
            var text = Encoding.UTF8.GetString(response);
            if (OnMessage != null)
              OnMessage(text, client);
            client.MessageBuffer.Clear();
          }
        }
      }
    } catch (Exception ex) {
      MelonLogger.Warning("Error with websocket: {0}", ex);
    } finally {
      CloseConnection(client);
    }
  }

  private void CloseConnection(Client client) {
    if (connectedClients.Contains(client)) {
      MelonLogger.Msg("Websocket disconnected");
      client.SendQueue.CompleteAdding();
      connectedClients.Remove(client);
    } else {
      client.TcpClient.Dispose();
    }
  }

  private void ListenToSendQueue(Client client) {
    try {
      var stream = client.TcpClient.GetStream();
      foreach (var data in client.SendQueue.GetConsumingEnumerable()) {
        if (!client.TcpClient.Connected)
          break;
        stream.Write(data, 0, data.Length);
      }
    } catch (Exception ex) {
      MelonLogger.Warning("Websocket error:");
      MelonLogger.Warning(ex);
    } finally {
      CloseConnection(client);
    }
  }

  private bool IsClientHandshake(byte[] request) =>
      Regex.IsMatch(Encoding.UTF8.GetString(request, 0, 4), "^GET\\s",
                    RegexOptions.IgnoreCase);

  private void RespondToClientHandshake(byte[] request, Client client) {
    var stream = client.TcpClient.GetStream();
    var requestString = Encoding.UTF8.GetString(request);
    var response = Encoding.UTF8.GetBytes(string.Join("\r\n", new[] {
      "HTTP/1.1 101 Switching Protocols",
      "Connection: Upgrade",
      "Upgrade: websocket",
      $"Sec-WebSocket-Accept: {GenerateWebsocketAcceptToken(requestString)}",
      "",
      "",
    }));
    stream.Write(response, 0, response.Length);
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

  private byte[] ReadMessage(byte[] request, Client client) {
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

    client.MessageBuffer.AddRange(decoded);

    if (!fin)
      return null;
    return client.MessageBuffer.ToArray();
  }

  private static byte[] CreateMessageFrame(byte[] data, bool isString) {
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
