using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sst.Common.Bonelab;

namespace Sst.Livesplit.BonelabHundredPercentStatus {
class BonelabStateUpdater : IDisposable {
  public event Action<HundredPercent.GameState> OnReceivedState;

  public HundredPercent.GameState State = new HundredPercent.GameState();
  public List<CompletionEvent> Events = new List<CompletionEvent>();

  private Common.Ipc.Client _client;

  public BonelabStateUpdater() {
    _client = new Common.Ipc.Client(HundredPercent.NAMED_PIPE);
    _client.OnMessageReceived += OnMessage;
    Console.WriteLine("Listening for Bonelab state change");
  }

  public void Dispose() { _client.Dispose(); }

  private void OnMessage(string message) {
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
  }

  private HundredPercent.GameState ParseLine(string line) {
    try {
      return JsonConvert.DeserializeObject<HundredPercent.GameState>(line);
    } catch (Exception err) {
      Console.Error.WriteLine(
          $"Error reading pipe message as JSON: {err.Message}");
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
