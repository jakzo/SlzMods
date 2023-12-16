using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sst.Common.Boneworks;
using Sst.Common.LiveSplit;

namespace Sst.Livesplit.BoneworksHundredStatus {
class BoneworksStateUpdater : IDisposable {
  public event Action<HundredPercentState> OnReceivedState;

  public HundredPercentState State = new HundredPercentState();

  private readonly Common.Ipc.Client _client;

  public BoneworksStateUpdater() {
    _client = new Common.Ipc.Client(HundredPercentState.NAMED_PIPE);
    _client.OnConnected += () => Log.Info("Connected");
    _client.OnDisconnected += () => Log.Info("Disconnected");
    _client.OnMessageReceived += OnMessage;
    Log.Info("Listening for Boneworks state change");
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

  private HundredPercentState ParseLine(string line) {
    try {
      return JsonConvert.DeserializeObject<HundredPercentState>(line);
    } catch (Exception err) {
      Log.Error($"Error reading pipe message as JSON: {err.Message}");
      return null;
    }
  }
}
}
