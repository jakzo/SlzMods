using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Text;

namespace Sst.Common.Ipc {
public class Client : IDisposable {
  public event Action OnConnected;
  public event Action OnDisconnected;
  public event Action<string> OnMessageReceived;

  public string Name;
  public NamedPipeClientStream Stream;

  private const int BUFFER_SIZE = 4096;

  private byte[] _buffer = new byte[BUFFER_SIZE];
  private bool _isDisposed = false;

  public Client(string name) {
    Name = name;
    Listen().ContinueWith(task => {
      if (task.Exception != null)
        Console.Error.WriteLine("Pipe client listen failed", task.Exception);
    });
  }

  public void Dispose() {
    if (_isDisposed)
      return;
    _isDisposed = true;
    CloseStream();
  }

  public async Task Send(string message) {
    var bytes = Encoding.UTF8.GetBytes(message);
    await Stream.WriteAsync(bytes, 0, bytes.Length);
  }

  private void CloseStream() {
    try {
      Stream.WaitForPipeDrain();
    } catch (Exception ex) {
      Console.Error.WriteLine("Failed to stop pipe client", ex);
    } finally {
      Stream.Close();
      Stream.Dispose();
      SafeInvoke(() => OnDisconnected?.Invoke());
    }
  }

  private async Task Listen() {
    while (!_isDisposed) {
      Stream = new NamedPipeClientStream(".", Name, PipeDirection.InOut,
                                         PipeOptions.Asynchronous);
      await Stream.ConnectAsync();
      if (_isDisposed)
        return;
      SafeInvoke(() => OnConnected?.Invoke());

      StringBuilder sb = null;
      while (true) {
        var numBytes = await Stream.ReadAsync(_buffer, 0, _buffer.Length);
        if (_isDisposed)
          return;
        if (numBytes <= 0) {
          CloseStream();
          break;
        }

        if (sb == null)
          sb = new StringBuilder();
        sb.Append(Encoding.UTF8.GetString(_buffer, 0, numBytes));

        if (Stream.IsMessageComplete) {
          var message = sb.ToString().TrimEnd('\0');
          SafeInvoke(() => OnMessageReceived?.Invoke(message));
          sb = null;
        }
      }
    }
  }

  private void SafeInvoke(Action action) {
    try {
      action();
    } catch (Exception ex) {
      Console.Error.WriteLine("Failed to run event", ex);
    }
  }
}
}
