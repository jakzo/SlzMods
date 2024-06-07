using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sst.Common.Boneworks;
using Sst.Common.LiveSplit;

namespace Sst.Livesplit.BoneworksDebugStats {
class StateUpdater : IDisposable {
  public event Action<DebugStats> OnReceivedState;

  public DebugStats State = new DebugStats();

  private Common.Ipc.Client _client;

  public StateUpdater() {
    _client = new Common.Ipc.Client(DebugStats.NAMED_PIPE);
    _client.OnConnected += () => Log.Info("Connected");
    _client.OnDisconnected += () => Log.Info("Disconnected");
    _client.OnMessageReceived += OnMessage;
    Log.Info("Listening for Boneworks debug state change");
  }

  public void Dispose() { _client.Dispose(); }

  private void OnMessage(string message) {
    try {
      Log.Info($"Received: {message}");
      var receivedState = ParseLine(message);
      if (receivedState == null)
        return;
      State = receivedState;
      OnReceivedState?.Invoke(receivedState);
    } catch (Exception ex) {
      Log.Error($"OnMessage error: {ex}");
    }
  }

  private DebugStats ParseLine(string line) {
    try {
      return JsonConvert.DeserializeObject<DebugStats>(line);
    } catch (Exception err) {
      Log.Error($"Error reading pipe message as JSON: {err.Message}");
      return null;
    }
  }
}
}
