using System;
using Newtonsoft.Json;
using MelonLoader;
using UnityEngine;

namespace Sst.Common.Bonelab.HundredPercent {
public class Server : IDisposable {
  public float ProgressUpdateFrequency;
  public GameProgress Progress;

  private Ipc.Server _ipcServer;
  private GameState _lastSentState;
  private float _lastUpdateTime = 0;

  public Server(float progressUpdateFrequency = 1f) {
    ProgressUpdateFrequency = progressUpdateFrequency;
    Progress = ProgressTracker.Calculate();
    MelonEvents.OnUpdate.Subscribe(UpdateProgressIfNecessary);

    AchievementTracker.OnUnlock += (id, name) => SendStateAfterUpdate(
        state => state.achievementsJustUnlocked = new[] { name });
    CapsuleTracker.OnUnlock += (id, name) => SendStateAfterUpdate(
        state => state.capsulesJustUnlocked = new[] { name });

    Utilities.LevelHooks.OnLoad += level => UpdateProgress();

    _ipcServer = new Ipc.Server(GameState.NAMED_PIPE);
    _ipcServer.OnClientConnected += stream => {
      Dbg.Log("OnClientConnected");
      var msg = BuildMessageToSend(_lastSentState ?? BuildGameState());
      Dbg.Log($"SendState on connect: {msg}");
      Ipc.Server.SendToStream(stream, msg);
    };
    _ipcServer.OnClientDisconnected +=
        stream => { Dbg.Log("OnClientDisconnected"); };
  }

  private void UpdateProgressIfNecessary() {
    if (!Utilities.LevelHooks.IsLoading &&
        Time.time - _lastUpdateTime >= ProgressUpdateFrequency)
      UpdateProgress();
  }

  private void UpdateProgress() {
    _lastUpdateTime = Time.time;
    Progress = ProgressTracker.Calculate();
    SendStateIfChanged();
  }

  private string BuildMessageToSend(GameState state) {
    _lastSentState = state;
    return JsonConvert.SerializeObject(state);
  }

  public void SendState(GameState state) {
    var msg = BuildMessageToSend(state);
    Dbg.Log($"SendState: {msg}");
    _ipcServer.Send(msg);
  }

  public void SendStateIfChanged(GameState state = null) {
    if (state == null)
      state = BuildGameState();
    if (IsStateDifferentFromLastSent(state))
      SendState(state);
  }

  private bool IsStateDifferentFromLastSent(GameState state) =>
      _lastSentState == null || state.isComplete != _lastSentState.isComplete
      || state.isLoading != _lastSentState.isLoading
      || state.levelBarcode != _lastSentState.levelBarcode
      || state.capsulesUnlocked != _lastSentState.capsulesUnlocked
      || state.capsulesTotal != _lastSentState.capsulesTotal
      || state.capsulesJustUnlocked != null
      || state.achievementsUnlocked != _lastSentState.achievementsUnlocked
      || state.achievementsTotal != _lastSentState.achievementsTotal
      || state.achievementsJustUnlocked != null
      || state.percentageComplete != _lastSentState.percentageComplete
      || state.percentageTotal != _lastSentState.percentageTotal;

  public GameState BuildGameState() => new GameState() {
    isComplete = false,
    isLoading = Utilities.LevelHooks.IsLoading,
    levelBarcode = (Utilities.LevelHooks.CurrentLevel ??
                    Utilities.LevelHooks.NextLevel)
                       ?.Barcode.ID,
    capsulesUnlocked = CapsuleTracker.Unlocked.Count,
    capsulesTotal = CapsuleTracker.NumTotalUnlocks,
    achievementsUnlocked = AchievementTracker.Unlocked.Count,
    achievementsTotal = AchievementTracker.NumPossibleAchievements,
    percentageComplete = Progress.Total,
  };

  private void SendStateAfterUpdate(Action<GameState> onStateCreated) {
    LemonAction onAfterUpdate = () => {
      var state = BuildGameState();
      onStateCreated(state);
      SendStateIfChanged(state);
    };
    MelonEvents.OnLateUpdate.Subscribe(onAfterUpdate,
                                       unsubscribeOnFirstInvocation: true);
  }

  public void Dispose() { _ipcServer.Dispose(); }
}
}
