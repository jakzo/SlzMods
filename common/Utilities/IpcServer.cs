using System;
using System.Linq;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sst.Common.Ipc {
public abstract class Logger {
  public abstract void Debug(string message);
  public abstract void Error(string message);
}

public class Server : IDisposable {
  public event Action<NamedPipeServerStream> OnClientConnected;
  public event Action<NamedPipeServerStream> OnClientDisconnected;
  public event Action<string> OnMessageReceived;

  public string Name;

  private const int BUFFER_SIZE = 4096;
  private const int MAX_NUMBER_OF_SERVER_INSTANCES = 10;

  private HashSet<NamedPipeServerStream> _streams =
      new HashSet<NamedPipeServerStream>();
  private bool _isDisposed = false;
  private Logger _logger;

  public Server(string name, Logger logger) {
    Name = name;
    _logger = logger;
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
    foreach (var stream in _streams.ToArray()) {
      try {
        SendToStream(stream, message);
      } catch (Exception ex) {
        _logger.Error("Error sending IPC message:");
        _logger.Error(ex.ToString());
        DisposeStream(stream);
      }
    }
  }

  public static void SendToStream(NamedPipeServerStream stream,
                                  string message) {
    if (!stream.IsConnected)
      return;

    var bytes = Encoding.UTF8.GetBytes(message);
    stream.Write(bytes, 0, bytes.Length);
  }

  private void StartNewPipeServerThread() { Task.Run(StartNewPipeServer); }

  private void StartNewPipeServer() {
    try {
      var stream = new NamedPipeServerStream(
          Name, PipeDirection.InOut, MAX_NUMBER_OF_SERVER_INSTANCES,
          PipeTransmissionMode.Message, PipeOptions.None, BUFFER_SIZE,
          BUFFER_SIZE);
      _streams.Add(stream);
      stream.WaitForConnection();
      _logger.Debug("Client connected");
      if (_isDisposed)
        return;
      SafeInvoke(() => OnClientConnected?.Invoke(stream));

      // TODO: stream.Read() blocks writes and many named pipe features are
      // missing from Mono
      // Task.Run(() => ReadFromPipeConnection(stream));
      // ReadFromPipeConnection2(stream);
      // Task.Run(() => PollPipeConnection(stream));
    } catch (Exception ex) {
      _logger.Error($"Pipe server failed: {ex}");
    } finally {
      if (!_isDisposed)
        StartNewPipeServerThread();
    }
  }

  private void ReadFromPipeConnection(NamedPipeServerStream stream) {
    var buffer = new byte[BUFFER_SIZE];
    StringBuilder sb = null;
    while (true) {
      if (sb == null)
        sb = new StringBuilder();
      var numBytes = stream.Read(buffer, 0, buffer.Length);
      if (_isDisposed)
        return;
      if (numBytes <= 0) {
        DisposeStream(stream);
        return;
      }

      sb.Append(Encoding.UTF8.GetString(buffer, 0, numBytes));

      if (stream.IsMessageComplete) {
        var message = sb.ToString().TrimEnd('\0');
        SafeInvoke(() => OnMessageReceived?.Invoke(message));
        sb = null;
      }
    }
  }

  private void ReadFromPipeConnection2(NamedPipeServerStream stream) {
    var buffer = new byte[BUFFER_SIZE];
    var sb = new StringBuilder();

    void ReadCallback(IAsyncResult ar) {
      try {
        int numBytes = stream.EndRead(ar);
        if (_isDisposed)
          return;
        if (numBytes <= 0) {
          DisposeStream(stream);
          return;
        }

        sb.Append(Encoding.UTF8.GetString(buffer, 0, numBytes));

        if (stream.IsMessageComplete) {
          var message = sb.ToString().TrimEnd('\0');
          SafeInvoke(() => OnMessageReceived?.Invoke(message));
          sb.Clear();
        }

        stream.BeginRead(buffer, 0, buffer.Length, ReadCallback, null);
      } catch (Exception ex) {
        _logger.Error("Error while reading from pipe connection:");
        _logger.Error(ex.ToString());
        DisposeStream(stream);
      }
    }

    try {
      stream.BeginRead(buffer, 0, buffer.Length, ReadCallback, null);
    } catch (Exception ex) {
      _logger.Error("Failed to begin read operation:");
      _logger.Error(ex.ToString());
      DisposeStream(stream);
    }
  }

  private void PollPipeConnection(NamedPipeServerStream stream) {
    var buffer = new byte[BUFFER_SIZE];
    var sb = new StringBuilder();

    try {
      while (true) {
        if (_isDisposed)
          return;

        if (stream.IsConnected) {
          while (stream.IsMessageComplete) {
            int numBytes = stream.Read(buffer, 0, buffer.Length);
            if (numBytes > 0) {
              sb.Append(Encoding.UTF8.GetString(buffer, 0, numBytes));
            } else {
              DisposeStream(stream);
              return;
            }

            if (stream.IsMessageComplete) {
              var message = sb.ToString().TrimEnd('\0');
              _logger.Debug("Finally got a message! " + message);
              SafeInvoke(() => OnMessageReceived?.Invoke(message));
              sb.Clear();
            }
          }
        }

        _logger.Debug("IPC waiting " + stream.IsConnected + " " +
                      stream.IsMessageComplete);
        Thread.Sleep(1000);
      }
    } catch (Exception ex) {
      _logger.Error("Error while reading from pipe:");
      _logger.Error(ex.ToString());
      DisposeStream(stream);
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
      _logger.Error($"Failed to stop pipe server: {ex}");
    } finally {
      stream.Close();
      stream.Dispose();
    }
  }

  private void SafeInvoke(Action action) {
    try {
      action();
    } catch (Exception ex) {
      _logger.Error($"Failed to run event: {ex}");
    }
  }
}
}
