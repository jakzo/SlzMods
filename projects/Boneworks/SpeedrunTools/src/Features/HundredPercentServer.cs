using System;
using UnityEngine;
using MelonLoader;
using Newtonsoft.Json;
using Sst.Common.Boneworks;
using Sst.Common.Ipc;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using StressLevelZero.Data;
using HarmonyLib;
using static Sst.Common.Boneworks.HundredPercentState;

namespace Sst.Features {
class HundredPercentServer : Feature {
  public static HundredPercentServer Instance;

  public HundredPercentState State;
  public Collectible[] LevelCollectibles;
  public Dictionary<string, int> LevelUncollectedIndexes;

  private Server _ipcServer;

  public HundredPercentServer() {
    IsAllowedInRuns = true;
    Instance = this;
  }

  public override void OnEnabled() {
    State = new HundredPercentState();
    LevelUncollectedIndexes = new Dictionary<string, int>();

    _ipcServer = new Server(NAMED_PIPE, new Logger());
    _ipcServer.OnClientConnected += stream => {
      var prevLevelCollectibles = State.levelCollectibles;
      var prevJustCollected = State.justCollected;

      State.levelCollectibles = LevelCollectibles;
      State.justCollected =
          LevelCollectibles
              ?.Where(c => LevelUncollectedIndexes.ContainsKey(c.Uuid))
              .ToArray();
      var msg = JsonConvert.SerializeObject(State);

      State.levelCollectibles = prevLevelCollectibles;
      State.justCollected = prevJustCollected;

      Dbg.Log("Send on connect:", msg);
      Server.SendToStream(stream, msg);
    };
    _ipcServer.OnClientDisconnected +=
        stream => { Dbg.Log("OnClientDisconnected"); };

    CollectibleRecorder.OnCollected += OnCollected;
  }

  private void OnCollected(Collectible collectible) {
    State.justCollected = [collectible];
    if (LevelUncollectedIndexes.ContainsKey(collectible.Uuid))
      LevelUncollectedIndexes.Remove(collectible.Uuid);
    SendState();
  }

  public override void OnLoadingScreen(int nextSceneIdx, int prevSceneIdx) {
    Task.Run(() => {
      try {
        LoadNextSceneRecording(nextSceneIdx);
      } catch (Exception ex) {
        MelonLogger.Error("Failed to load next scene recording:");
        MelonLogger.Error(ex);
      }
    });
  }

  private void LoadNextSceneRecording(int nextSceneIdx) {
    State.levelCollectibles =
        CollectibleRecorder.Instance.LoadRecording(nextSceneIdx);
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    if (State.levelCollectibles == null)
      State.levelCollectibles = [];

    var levelUnlocks =
        State.levelCollectibles.Where(c => c.Type == TYPE_ITEM).ToArray();
    LevelUncollectedIndexes = new Dictionary<string, int>();
    for (var i = 0; i < levelUnlocks.Length; i++) {
      var collectible = levelUnlocks[i];
      if (!LevelUncollectedIndexes.ContainsKey(collectible.Uuid) &&
          !ReclaimerData._reclaimedObjects.ContainsKey(collectible.Uuid)) {
        LevelUncollectedIndexes.Add(collectible.Uuid, i);
      }
    }

    State.unlockLevelMax = levelUnlocks.Length;
    State.ammoLevelMax =
        State.levelCollectibles.Where(c => IsTypeAmmo(c.Type)).Count();
    State.ammoLevelCount = 0;

    // Avoid reporting collectibles collected in a previous level as missing
    State.levelCollectibles =
        State.levelCollectibles
            .Where(c => !ReclaimerData._reclaimedObjects.ContainsKey(c.Uuid))
            .ToArray();

    SendState();
  }

  public override void OnDisabled() {
    State = null;
    LevelCollectibles = null;
    LevelUncollectedIndexes = null;

    _ipcServer.Dispose();
    _ipcServer = null;

    CollectibleRecorder.OnCollected -= OnCollected;
  }

  public void Reset() {
    if (!IsEnabled)
      return;
    State = new HundredPercentState();
    LevelUncollectedIndexes = new Dictionary<string, int>();
    LevelCollectibles = null;
  }

  public override void OnLevelStart(int sceneIdx) {}

  public override void OnUpdate() {}

  public void SendState() {
    State.unlockLevelCount =
        State.unlockLevelMax - LevelUncollectedIndexes.Count;

    if (State.justCollected != null) {
      foreach (var c in State.justCollected) {
        if (IsTypeAmmo(c.Type))
          State.ammoLevelCount++;
      }
    }

    var msg = JsonConvert.SerializeObject(State);

    State.justCollected = null;
    if (State.levelCollectibles != null) {
      LevelCollectibles = State.levelCollectibles;
      State.levelCollectibles = null;
    }

    Dbg.Log("Sending state:", msg);
    Task.Run(() => _ipcServer.Send(msg));
  }

  private bool IsTypeAmmo(string type) => type == TYPE_AMMO_LIGHT
      || type == TYPE_AMMO_MEDIUM;

  // This can be slightly inaccurate if not using the latest LootDropBugfix mod
  [HarmonyPatch(typeof(LootTableData), nameof(LootTableData.GetLootItem))]
  class LootTableData_GetLootItem_Patch {
    [HarmonyPostfix()]
    internal static void
    Postfix(LootTableData __instance, SpawnableObject __result) {
      try {
        if (Instance.State == null || __result == null)
          return;

        var seenUuids = new HashSet<string>();
        var lower = 0f;
        foreach (var item in __instance.items) {
          var upper = Mathf.Min(lower + item.percentage, 100f);
          var uuid = item.spawnable.UUID;

          if (item.percentage > 0f &&
              Instance.State.rngUnlocks.TryGetValue(uuid, out var rngState) &&
              !rngState.hasDropped && !seenUuids.Contains(uuid)) {
            seenUuids.Add(uuid);
            rngState.attempts++;
            rngState.prevAttemptChance = (upper - lower) / 100f;
            rngState.probabilityNotDroppedYet *=
                1f - rngState.prevAttemptChance;
            if (__result.UUID == uuid)
              rngState.hasDropped = true;
          }

          if (upper >= 100f)
            break;
          lower = upper;
        }

        if (seenUuids.Count > 0)
          Instance.SendState();
      } catch (Exception ex) {
        MelonLogger.Error("Error in LootTableData_GetLootItem_Patch:");
        MelonLogger.Error(ex);
      }
    }
  }

  class Logger : Common.Ipc.Logger {
    public override void Debug(string message) => Dbg.Log(message);
    public override void Error(string message) => MelonLogger.Error(message);
  }
}
}
