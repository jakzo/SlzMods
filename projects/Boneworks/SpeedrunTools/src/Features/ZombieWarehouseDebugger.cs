using System.Linq;
using UnityEngine;
using MelonLoader;
using StressLevelZero.Props;
using StressLevelZero;
using StressLevelZero.UI;
using StressLevelZero.Rig;
using StressLevelZero.Combat;
using StressLevelZero.Pool;
using StressLevelZero.Props.Weapons;
using StressLevelZero.Data;
using HarmonyLib;
using PuppetMasta;
using System.Collections.Generic;
using StressLevelZero.Arena;

namespace Sst.Features {
class ZombieWarehouseDebugger : Feature {
  public static ZombieWarehouseDebugger Instance;
  public readonly Pref<bool> PrefLogHooks = new Pref<bool>() {
    Id = "zombieWarehouseDebuggerLogHooks",
    Name = "Enable logging from hooks in Zombie Warehouse Debugger",
    DefaultValue = false,
  };
  private static bool killSecondEnemy = false;
  private static bool killAllEnemies = false;
  private static bool waitingToKillAll = false;
  private static int lastSpawnedCount = 0;
  private static float timeSinceLastSpawn = 0f;
  private static bool isWaveOngoing = false;
  private static int maxActiveEnemies = 0;
  private static int killsAtWaveStart = 0;
  private static Zombie_GameControl gameControl;
  private static readonly int ZOMBIE_WAREHOUSE_IDX =
      Utils.SCENE_INDEXES_BY_NAME["zombie_warehouse"];

  public ZombieWarehouseDebugger() {
    Instance = this;
    IsDev = true;

    // Toggle kill all enemies
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.GameState.currentSceneIdx == ZOMBIE_WAREHOUSE_IDX &&
          Input.GetKeyDown(KeyCode.Alpha1),
      Handler =
          () => {
            waitingToKillAll = true;
            lastSpawnedCount = 0;
            timeSinceLastSpawn = 0f;
            Log("Waiting for all zombies to spawn before killing...");
          },
    });

    // Toggle kill second enemy
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.GameState.currentSceneIdx == ZOMBIE_WAREHOUSE_IDX &&
          Input.GetKeyDown(KeyCode.Alpha2),
      Handler =
          () => {
            killSecondEnemy = true;
            Log("Will kill second enemy of wave");
          },
    });
  }

  public override void OnLevelStart(int sceneIdx) {
    if (sceneIdx != ZOMBIE_WAREHOUSE_IDX)
      return;

    gameControl = Object.FindObjectOfType<Zombie_GameControl>();
    isWaveOngoing = false;
    killAllEnemies = false;
    killSecondEnemy = false;
    waitingToKillAll = false;
    lastSpawnedCount = 0;
    timeSinceLastSpawn = 0f;
    maxActiveEnemies = 0;
  }

  public override void OnUpdate() {
    if (Mod.GameState.currentSceneIdx != ZOMBIE_WAREHOUSE_IDX)
      return;

    if (gameControl == null)
      gameControl = Object.FindObjectOfType<Zombie_GameControl>();
    if (gameControl?.activeEnemyList == null) {
      return;
    }

    if (isWaveOngoing && !gameControl.isWaveOngoing) {
      // Log second zombie spawn location before wave starts
      var secondSpawnIdx =
          (gameControl.currSpawn_floor1 + 2) % spawnPointDescriptions.Count;
      var secondSpawnPointDesc = secondSpawnIdx < spawnPointDescriptions.Count
          ? spawnPointDescriptions[secondSpawnIdx]
          : "unknown";
      var killsThisWave = gameControl.killCount - killsAtWaveStart;
      Log($"Wave {gameControl.currWaveIndex} ended. maxActiveEnemies = {maxActiveEnemies}, killCount = {gameControl.killCount}, killsThisWave = {killsThisWave}. Second zombie will spawn from: {secondSpawnPointDesc}"
      );

      isWaveOngoing = false;
      killAllEnemies = false;
      killSecondEnemy = false;
      waitingToKillAll = false;
      lastSpawnedCount = 0;
      timeSinceLastSpawn = 0f;
      maxActiveEnemies = 0;
      killsAtWaveStart = gameControl.killCount;

      return;
    }

    isWaveOngoing = gameControl.isWaveOngoing;
    if (maxActiveEnemies < gameControl.activeEnemyCount) {
      maxActiveEnemies = gameControl.activeEnemyCount;
    }

    static void Kill(Arena_EnemyReference enemy) {
      if (enemy == null)
        return;
      enemy.KillEnemy();
      var killCount = gameControl.killCount;
      var activeEnemyCount = gameControl.activeEnemyCount;
      Log($"Killed enemy (killCount = {killCount}, activeEnemyCount = {activeEnemyCount})"
      );
    }

    if (gameControl.activeEnemyCount <= 0) {
      timeSinceLastSpawn = 0f;
    } else if (waitingToKillAll) {
      if (gameControl.activeEnemyCount > lastSpawnedCount) {
        lastSpawnedCount = gameControl.activeEnemyCount;
        timeSinceLastSpawn = 0f;
        // Log($"Zombie spawned, count now: {gameControl.activeEnemyCount}");
      } else {
        timeSinceLastSpawn += Time.deltaTime;
      }

      // Wait 3 seconds after last spawn to ensure no more are spawning
      if (timeSinceLastSpawn >= 3f && gameControl.activeEnemyCount > 0) {
        Log($"All {gameControl.activeEnemyCount} zombies spawned. Killing them now!"
        );
        killAllEnemies = true;
        waitingToKillAll = false;
        timeSinceLastSpawn = 0f;
      }
    }

    if (killAllEnemies) {
      for (int i = gameControl.activeEnemyList.Count - 1; i >= 0; i--) {
        var enemy = gameControl.activeEnemyList[i];
        Kill(enemy);
      }
    }

    if (killSecondEnemy && gameControl.activeEnemyList.Count == 2) {
      var enemy = gameControl.activeEnemyList[1];
      if (enemy != null) {
        var puppet = enemy.GetComponentInChildren<PuppetMaster>();
        if (puppet != null) {
          puppet.Kill();
          Log($"Killed second enemy (killCount = {gameControl.killCount}, activeEnemyCount = {gameControl.activeEnemyCount})"
          );
        }
      }
      killSecondEnemy = false;
    }
  }

  // public override void OnLevelStart(int sceneIdx) {
  //   if (sceneIdx != Sst.ZOMBIE_WAREHOUSE_IDX)
  //     return;

  //   var ui = GameObject.FindObjectOfType<MainMenuUIController>().transform;
  //   var head =
  //       UnityEngine.GameObject.FindObjectOfType<RigManager>().physicsRig.m_head;
  //   ui.SetParent(head);
  //   ui.FindChild("Interactable").gameObject.SetActive(false);
  //   ui.FindChild("ART").gameObject.SetActive(false);
  //   ui.localPosition = new Vector3(0f, 0f, 3f);
  //   ui.localRotation = Quaternion.EulerAngles(4.5f, 0f, 0f);
  //   ui.localScale = Vector3.one * 1f;
  // }

  // private static int _puppetDeathStack = 0;
  // private static void
  // LogState(Zombie_GameControl gameControl, string type, PuppetMaster puppet)
  // {
  //   var enemyGameObject = puppet.transform.parent.gameObject;
  //   Dbg.Log(string.Join("\n", [
  //     "", $"=== {type} ===", $"PuppetDeathStack: {_puppetDeathStack}",
  //     $"activeEnemyCount: {gameControl.activeEnemyCount}",
  //     $"name: {enemyGameObject.name} (ID:
  //     {enemyGameObject.GetInstanceID()})",
  //     $"activeEnemyList: {gameControl.activeEnemyList.Count}",
  //     $"addEnemyCount: {gameControl.addEnemyCount}",
  //     $"totalEnemyCount: {gameControl.totalEnemyCount}",
  //     $"totalKillCount: {gameControl.totalKillCount}",
  //     $"killCount: {gameControl.killCount}", ""
  //   ]));
  // }

  // [HarmonyPatch(
  //     typeof(Zombie_GameControl), nameof(Zombie_GameControl.OnPuppetDeath)
  // )]
  // class Zombie_GameControl_OnPuppetDeath_Patch {
  //   [HarmonyPrefix()]
  //   internal static void
  //   Prefix(Zombie_GameControl __instance, PuppetMaster puppet) {
  //     LogState(__instance, "OnPuppetDeath Prefix", puppet);
  //     _puppetDeathStack++;
  //   }

  //   [HarmonyPostfix()]
  //   internal static void
  //   Postfix(Zombie_GameControl __instance, PuppetMaster puppet) {
  //     _puppetDeathStack--;
  //     LogState(__instance, "OnPuppetDeath Postfix", puppet);
  //   }
  // }

  private static void Log(string message) {
    if (Instance.IsEnabled)
      Dbg.Log($"ZOMBO: {message}");
  }

  private static void LogHook(string message) {
    if (Instance.PrefLogHooks.Read())
      Log(message);
  }

  [HarmonyPatch(
      typeof(Zombie_GameControl), nameof(Zombie_GameControl.OnPuppetDeath)
  )]
  class Zombie_GameControl_OnPuppetDeath_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(Zombie_GameControl __instance) {
      if (__instance.activeEnemyCount == 2) {
        LogHook(
            "puppet death with 2 enemies active, single enemy timer SHOULD start now!"
        );
      }
    }
  }

  [HarmonyPatch(
      typeof(Zombie_GameControl), nameof(Zombie_GameControl.CoSingleEnemyTimer)
  )]
  class Zombie_GameControl_CoSingleEnemyTimer_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(Zombie_GameControl __instance) {
      LogHook("single enemy timer STARTED!");
    }
  }

  [HarmonyPatch(
      typeof(Zombie_GameControl), nameof(Zombie_GameControl.ForceWaveEnd)
  )]
  class Zombie_GameControl_ForceWaveEnd_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(Zombie_GameControl __instance) {
      LogHook("wave FORCE ENDED!");
    }
  }

  private static readonly List<string> spawnPointDescriptions =
      new List<string>() {
        "near-left",
        "near-right",
        "far-left",
        "far-right",
      };

  [HarmonyPatch(
      typeof(Zombie_GameControl), nameof(Zombie_GameControl.GetSpawnPoint)
  )]
  class Zombie_GameControl_GetSpawnPoint_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(Zombie_GameControl __instance) {
      var nextSpawnIdx =
          (__instance.currSpawn_floor1 + 1) % spawnPointDescriptions.Count;
      var spawnPointDesc = __instance.insideSpawnA
          ? nextSpawnIdx < spawnPointDescriptions.Count
              ? spawnPointDescriptions[nextSpawnIdx]
              : "unknown"
          : "not in zone A";
      LogHook(
          $"currSpawn_floor1 = {__instance.currSpawn_floor1} (next spawn point is {spawnPointDesc})"
      );
    }
  }
}
}
