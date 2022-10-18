using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SLZ.Rig;
using SLZ.Marrow.Warehouse;

namespace Sst {
class Utils {
  public const string PREF_CATEGORY = "SpeedrunTools";

  public const string LEVEL_TITLE_DESCENT = "01 - Descent";
  public const string LEVEL_TITLE_HOME = "14 - Home";
  public const string LOADING_SCENE_NAME = "77da2b1cce998aa4fb4fc76a7fd80e05";

  public Dictionary<string, Level> Levels =
      new[] {
        new Level() {
          Id = "",
          DisplayName = "Descent",
        },
      }
          .ToDictionary(level => level.Id);

  public class Level {
    public string Id;
    public string DisplayName;
  }

  public static MelonPreferences_Category s_prefCategory;

  public static readonly Pref<bool> PrefDebug =
      new Pref<bool>() { Id = "printDebugLogs",
                         Name = "Print debug logs to console",
                         DefaultValue = false };

  public static UnityEngine.Vector3 GetPlayerPos() {
    return Mod.GameState.rigManager.physicsRig.m_head.position;
  }

  public static void LogDebug(string msg, params object[] data) {
    if (PrefDebug.Read())
      MelonLogger.Msg($"dbg: {msg}", data);
  }

  public static bool GetKeyControl() =>
      UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) ||
      UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl);

  public static string DurationToString(TimeSpan duration) => duration.ToString(
      $"{(duration.Seconds >= 60 * 60 ? "h\\:m" : "")}m\\:ss\\.ff");
}

class Pref<T> : IPref {
  public string Id { get; set; }
  public string Name;
  public string Description;
  public T DefaultValue;

  public void Create() {
    Utils.s_prefCategory.CreateEntry(Id, DefaultValue, Name, Description);
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
  private BaseController[] _controllers;

  public void Init() {
    var controllerObjects =
        UnityEngine.Object.FindObjectsOfType<BaseController>();
    _controllers =
        new string[] { "left", "right" }
            .Select(type => $"Hand ({type})")
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
                return new BaseController();
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
  public Func<BaseController, BaseController, bool> Predicate { get; set; }
  public Action Handler { get; set; }
}

abstract public class Feature {
  public List<Hotkey> Hotkeys = new List<Hotkey>();
  public bool IsAllowedInRuns = false;
  public bool IsEnabled = false;
  public bool IsEnabledByDefault = true;
  public bool IsDev = false;
  public virtual void OnInitialize() {}
  public virtual void OnLoadingScreen(LevelCrate nextLevel,
                                      LevelCrate prevLevel) {}
  public virtual void OnLevelStart(LevelCrate level) {}
  public virtual void OnUpdate() {}
  public virtual void OnFixedUpdate() {}
  public virtual void OnEnabled() {}
  public virtual void OnDisabled() {}
}

public struct GameState {
  public LevelCrate prevLevel;
  public LevelCrate currentLevel;
  public LevelCrate nextLevel;
  public bool didPrevLevelComplete;
  public RigManager rigManager;
}
}
