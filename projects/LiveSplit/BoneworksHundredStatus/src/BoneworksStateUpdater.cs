using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Sst.Common.Boneworks;
using Sst.Common.LiveSplit;

namespace Sst.Livesplit.BoneworksHundredStatus {
class BoneworksStateUpdater : IDisposable {
  public event Action<HundredPercentState> OnReceivedState;

  public HundredPercentState State;
  public HundredPercentState.Collectible[] LevelCollectibles;
  public Dictionary<string, int> LevelCollectableIndexes;

  private readonly Common.Ipc.Client _client;
  private readonly JavaScriptSerializer _serializer;

  public BoneworksStateUpdater() {
    _serializer = new JavaScriptSerializer();
    _client = new Common.Ipc.Client(HundredPercentState.NAMED_PIPE);
    _client.OnConnected += () => {
      Log.Info("Connected");
      State = new HundredPercentState();
      OnReceivedState?.Invoke(State);
    };
    _client.OnDisconnected += () => {
      Log.Info("Disconnected");
      State = null;
      OnReceivedState?.Invoke(State);
    };
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
      if (State.levelCollectibles != null) {
        LevelCollectibles = State.levelCollectibles;
        LevelCollectableIndexes = new Dictionary<string, int>();
        for (var i = 0; i < LevelCollectibles.Length; i++) {
          var collectible = LevelCollectibles[i];
          if (!LevelCollectableIndexes.ContainsKey(collectible.Uuid)) {
            LevelCollectableIndexes.Add(collectible.Uuid, i);
          }
        }
      }
      OnReceivedState?.Invoke(State);
    } catch (Exception ex) {
      Log.Error($"OnMessage error: {ex}");
    }
  }

  private HundredPercentState ParseLine(string line) {
    try {
      return _serializer.Deserialize<HundredPercentState>(line);
    } catch (Exception err) {
      Log.Error($"Error reading pipe message as JSON: {err.Message}");
      return null;
    }
  }
}
}
