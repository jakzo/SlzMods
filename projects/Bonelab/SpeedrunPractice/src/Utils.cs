using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using SLZ.Rig;
using SLZ.Marrow.Warehouse;

namespace Sst.SpeedrunPractice {
public static class Utils {
  public const string PREF_CATEGORY = "SpeedrunPractice";
  public static readonly string REPLAYS_DIR =
      Path.Combine(MelonUtils.UserDataDirectory, "SpeedrunPractice_Replays");

  public static MelonPreferences_Category s_prefCategory;

  public static GameState State;

  public static Vector3 GetPlayerPos() {
    return State.rigManager.realHeptaRig.transform.position;
  }

  public class PlayerState {
    public Vector3 pos;
  }

  public static PlayerState GetPlayerState() {
    return new PlayerState() {
      pos = Utils.GetPlayerPos(),
    };
  }
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
  public bool IsEnabled = false;
  public bool IsEnabledByDefault = true;
  public bool IsDev = false;
  public virtual void OnApplicationStart() {}
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
