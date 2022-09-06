using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpeedrunTools {
class Utils {
  public const string PREF_CATEGORY = "SpeedrunTools";
  public static readonly string DIR =
      Path.Combine(MelonUtils.UserDataDirectory, "SpeedrunTools");
  public static readonly string REPLAYS_DIR = Path.Combine(DIR, "replays");
  public const int SCENE_MENU_IDX = 1;
  public const string SCENE_INTRO_NAME = "scene_theatrigon_movie01";
  public static readonly Dictionary<string, int> SCENE_INDEXES_BY_NAME =
      new Dictionary<string, int>() {
        ["scene_introStart"] = 0,
        ["scene_mainMenu"] = SCENE_MENU_IDX,
        ["scene_MainMenu"] = SCENE_MENU_IDX,
        ["Main Menu"] = SCENE_MENU_IDX,
        [SCENE_INTRO_NAME] = 2,
        ["scene_breakroom"] = 3,
        ["Breakroom"] = 3,
        ["scene_museum"] = 4,
        ["Museum"] = 4,
        ["scene_streets"] = 5,
        ["Streets"] = 5,
        ["scene_runoff"] = 6,
        ["Runoff"] = 6,
        ["scene_sewerStation"] = 7,
        ["Sewers"] = 7,
        ["scene_warehouse"] = 8,
        ["Warehouse"] = 8,
        ["scene_subwayStation"] = 9,
        ["Central Station"] = 9,
        ["scene_tower"] = 10,
        ["Tower"] = 10,
        ["scene_towerBoss"] = 11,
        ["Time Tower"] = 11,
        ["scene_theatrigon_movie02"] = 12,
        ["scene_dungeon"] = 13,
        ["Dungeon"] = 13,
        ["scene_arena"] = 14,
        ["Arena"] = 14,
        ["scene_throneRoom"] = 15,
        ["Throne Room"] = 15,
        ["arena_fantasy"] = 16,
        ["scene_Tuscany"] = 17,
        ["scene_redactedChamber"] = 18,
        ["sandbox_handgunBox"] = 19,
        ["sandbox_museumBasement"] = 20,
        ["sandbox_blankBox"] = 21,
        ["scene_hoverJunkers"] = 22,
        ["zombie_warehouse"] = 23,
        ["empty_scene"] = 24,
        ["loadingScene"] = 25,
      };

  public static MelonPreferences_Category s_prefCategory;

  public static readonly Pref<bool> PrefDebug =
      new Pref<bool>() { Id = "printDebugLogs",
                         Name = "Print debug logs to console",
                         DefaultValue = false };

  public static UnityEngine.Vector3
  GetPlayerPos(StressLevelZero.Rig.RigManager rigManager) {
    return rigManager.ControllerRig.transform.position;
  }

  public static void LogDebug(string msg, params object[] data) {
    if (PrefDebug.Read())
      MelonLogger.Msg($"dbg: {msg}", data);
  }

  public static bool GetKeyControl() =>
      UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) ||
      UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl);
}

class Pref<T> : IPref {
  public string Id { get; set; }
  public string Name;
  public T DefaultValue;

  public void Create() {
    Utils.s_prefCategory.CreateEntry(Id, DefaultValue, Name);
  }

  public T Read() { return Utils.s_prefCategory.GetEntry<T>(Id).Value; }
}
interface IPref {
  string Id { get; }
  void Create();
}

public class Hotkeys {
  private Dictionary<Hotkey, (Feature, bool)> _hotkeys =
      new Dictionary<Hotkey, (Feature, bool)>();
  private StressLevelZero.Rig.BaseController[] _controllers;

  public void Init() {
    var controllerObjects =
        UnityEngine.Object
            .FindObjectsOfType<StressLevelZero.Rig.BaseController>();
    _controllers =
        new string[] { "left", "right" }
            .Select(type => $"Controller ({type})")
            .Select(name => {
              try {
                return controllerObjects.First(controller =>
                                                   controller.name == name);
              } catch {
                var foundControllers = string.Join(
                    "", controllerObjects.Select(controller =>
                                                     $"\n- {controller.name}"));
                MelonLogger.Warning(
                    $"Could not find {name}. Hotkeys will not work until reloading the level. Found controllers are:{foundControllers}");
                return new StressLevelZero.Rig.BaseController();
              }
            })
            .ToArray();
  }

  public void AddHotkey(Feature feature, Hotkey hotkey) {
    if (_hotkeys.ContainsKey(hotkey))
      return;
    _hotkeys.Add(hotkey, (feature, false));
  }

  public void RemoveHotkey(Hotkey hotkey) { _hotkeys.Remove(hotkey); }

  public void OnUpdate() {
    if (_controllers == null || _controllers.Length < 2)
      return;

    var entries = _hotkeys
                      .Select((entry, i) => (i, entry.Key, entry.Value.Item1,
                                             entry.Value.Item2))
                      .ToArray();
    foreach (var (i, hotkey, feature, isDown) in entries) {
      if (Mod.IsRunActive && !feature.IsAllowedInRuns)
        continue;
      if (hotkey.Predicate(_controllers[0], _controllers[1])) {
        if (isDown)
          continue;
        _hotkeys[hotkey] = (feature, true);
        hotkey.Handler();
      } else {
        _hotkeys[hotkey] = (feature, false);
      }
    }
  }
}

public class Hotkey {
  public Func<StressLevelZero.Rig.BaseController,
              StressLevelZero.Rig.BaseController, bool> Predicate { get; set; }
  public Action Handler { get; set; }
}

abstract public class Feature {
  public List<Hotkey> Hotkeys = new List<Hotkey>();
  public bool IsAllowedInRuns = false;
  public bool IsEnabled = false;
  public bool IsEnabledByDefault = true;
  public bool IsDev = false;
  public virtual void OnApplicationStart() {}
  public virtual void OnLoadingScreen(int nextSceneIdx, int prevSceneIdx) {}
  public virtual void OnLevelStart(int sceneIdx) {}
  public virtual void OnSceneWasLoaded(int buildIndex, string sceneName) {}
  public virtual void OnSceneWasInitialized(int buildIndex, string sceneName) {}
  public virtual void OnUpdate() {}
  public virtual void OnFixedUpdate() {}
  public virtual void OnEnabled() {}
  public virtual void OnDisabled() {}
}

public struct GameState {
  public int? prevSceneIdx;
  public int? currentSceneIdx;
  public int? nextSceneIdx;
}
}
