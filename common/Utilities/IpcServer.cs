using System;
using System.Linq;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;

namespace Sst.Common.Ipc {
public class Server : IDisposable {
  public event Action<NamedPipeServerStream> OnClientConnected;
  public event Action<NamedPipeServerStream> OnClientDisconnected;
  // public event Action<string> OnMessageReceived;

  public string Name;

  private const int BUFFER_SIZE = 4096;
  private const int MAX_NUMBER_OF_SERVER_INSTANCES = 10;

  private HashSet<NamedPipeServerStream> _streams =
      new HashSet<NamedPipeServerStream>();
  private bool _isDisposed = false;

  public Server(string name) {
    Name = name;
    StartNewPipeServerThread();
  }

  public void Dispose() {
    if (_isDisposed)
      return;
    _isDisposed = true;
    foreach (var stream in _streams.ToArray())
      DisposeStream(stream);
  }

  public void Send(string message) {
    foreach (var stream in _streams.ToArray())
      SendToStream(stream, message);
  }

  public static void SendToStream(NamedPipeServerStream stream,
                                  string message) {
    if (!stream.IsConnected)
      return;
    var bytes = Encoding.UTF8.GetBytes(message);
    stream.Write(bytes, 0, bytes.Length);
  }

  private void StartNewPipeServerThread() {
    new System.Threading.Thread(StartNewPipeServer).Start();
  }

  private void StartNewPipeServer() {
    try {
      var stream = new NamedPipeServerStream(Name, PipeDirection.InOut,
                                             MAX_NUMBER_OF_SERVER_INSTANCES,
                                             PipeTransmissionMode.Message);
      _streams.Add(stream);
      stream.WaitForConnection();
      Dbg.Log("Client connected");
      if (_isDisposed)
        return;
      StartNewPipeServerThread();
      SafeInvoke(() => OnClientConnected?.Invoke(stream));

      // TODO: The stream.Read() call blocks writes
      // var buffer = new byte[BUFFER_SIZE];
      // StringBuilder sb = null;
      // while (true) {
      //   if (sb == null)
      //     sb = new StringBuilder();
      //   var numBytes = stream.Read(buffer, 0, buffer.Length);
      //   if (_isDisposed)
      //     return;
      //   if (numBytes <= 0) {
      //     DisposeStream(stream);
      //     return;
      //   }

      //   sb.Append(Encoding.UTF8.GetString(buffer, 0, numBytes));

      //   if (stream.IsMessageComplete) {
      //     var message = sb.ToString().TrimEnd('\0');
      //     SafeInvoke(() => OnMessageReceived?.Invoke(message));
      //     sb = null;
      //   }
      // }
    } catch (Exception ex) {
      MelonLogger.Error($"Pipe server failed: {ex.ToString()}");
    }
  }

  private void DisposeStream(NamedPipeServerStream stream) {
    _streams.Remove(stream);
    try {
      if (stream.IsConnected) {
        stream.Disconnect();
        SafeInvoke(() => OnClientDisconnected?.Invoke(stream));
      }
    } catch (Exception ex) {
      MelonLogger.Error($"Failed to stop pipe server: {ex.ToString()}");
    } finally {
      stream.Close();
      stream.Dispose();
    }
  }

  private void SafeInvoke(Action action) {
    try {
      action();
    } catch (Exception ex) {
      MelonLogger.Error($"Failed to run event: {ex.ToString()}");
    }
  }
}
}
