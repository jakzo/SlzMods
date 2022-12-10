using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sst.Common.Bonelab.HundredPercent;
using Sst.Common.LiveSplit;

namespace Sst.Livesplit.BonelabHundredStatus {
class BonelabStateUpdater : IDisposable {
  public event Action<GameState> OnReceivedState;

  public GameState State = new GameState();
  public List<CompletionEvent> Events = new List<CompletionEvent>();

  private Common.Ipc.Client _client;

  public BonelabStateUpdater() {
    _client = new Common.Ipc.Client(GameState.NAMED_PIPE);
    _client.OnConnected += () => Log.Info("Connected");
    _client.OnDisconnected += () => Log.Info("Disconnected");
    _client.OnMessageReceived += OnMessage;
    Log.Info("Listening for Bonelab state change");
  }

  public void Dispose() { _client.Dispose(); }

  private void OnMessage(string message) {
    try {
      Log.Info($"Received: {message}");
      var receivedState = ParseLine(message);
      if (receivedState == null)
        return;
      State = receivedState;
      if (receivedState.capsulesJustUnlocked != null)
        foreach (var name in receivedState.capsulesJustUnlocked)
          Events.Add(new CompletionEvent() {
            time = DateTime.Now,
            type = CompletionEventType.CAPSULE,
            name = name,
          });
      if (receivedState.achievementsJustUnlocked != null)
        foreach (var name in receivedState.achievementsJustUnlocked)
          Events.Add(new CompletionEvent() {
            time = DateTime.Now,
            type = CompletionEventType.ACHIEVEMENT,
            name = name,
          });
      OnReceivedState?.Invoke(receivedState);
    } catch (Exception ex) {
      Log.Error($"ONMessage error: {ex.ToString()}");
    }
  }

  private GameState ParseLine(string line) {
    try {
      return JsonConvert.DeserializeObject<GameState>(line);
    } catch (Exception err) {
      Log.Error($"Error reading pipe message as JSON: {err.Message}");
      return null;
    }
  }

  public class CompletionEvent {
    public DateTime time;
    public CompletionEventType type;
    public string name;
  }

  public enum CompletionEventType {
    CAPSULE,
    ACHIEVEMENT,
  }
}
}
