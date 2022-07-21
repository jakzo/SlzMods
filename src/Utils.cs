using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpeedrunTools
{
  class Utils
  {
    public const string PREF_CATEGORY = "SpeedrunTools";
    public static readonly string DIR = Path.Combine(MelonUtils.UserDataDirectory, "SpeedrunTools");
    public static readonly string REPLAYS_DIR = Path.Combine(DIR, "replays");
    public const int SCENE_MENU_IDX = 1;

    public static MelonPreferences_Category s_prefCategory;

    public static readonly Pref<bool> PrefDebug = new Pref<bool>()
    {
      Id = "printDebugLogs",
      Name = "Print debug logs to console",
      DefaultValue = false
    };

    public static UnityEngine.Vector3 GetPlayerPos(StressLevelZero.Rig.RigManager rigManager)
    {
      return rigManager.ControllerRig.transform.position;
    }

    public static void LogDebug(string msg, params object[] data)
    {
      if (PrefDebug.Read())
        MelonLogger.Msg($"dbg: {msg}", data);
    }

    public static bool GetKeyControl() =>
      UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) ||
      UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl);
  }

  class Pref<T> : IPref
  {
    public string Id { get; set; }
    public string Name;
    public T DefaultValue;

    public void Create()
    {
      Utils.s_prefCategory.CreateEntry(Id, DefaultValue, Name);
    }

    public T Read()
    {
      return Utils.s_prefCategory.GetEntry<T>(Id).Value;
    }
  }
  interface IPref
  {
    string Id { get; }
    void Create();
  }

  public class Hotkeys
  {
    private Dictionary<Hotkey, (Feature, bool)> _hotkeys =
      new Dictionary<Hotkey, (Feature, bool)>();
    private StressLevelZero.Rig.BaseController[] _controllers;

    public void Init()
    {
      var controllerObjects = UnityEngine.Object.FindObjectsOfType<StressLevelZero.Rig.BaseController>();
      _controllers = new string[] { "left", "right" }
        .Select(type => $"Controller ({type})")
        .Select(name => controllerObjects.First(controller => controller.name == name))
        .Where(controller => controller != null)
        .ToArray();
    }

    public void AddHotkey(Feature feature, Hotkey hotkey)
    {
      if (_hotkeys.ContainsKey(hotkey)) return;
      _hotkeys.Add(hotkey, (feature, false));
    }

    public void RemoveHotkey(Hotkey hotkey)
    {
      _hotkeys.Remove(hotkey);
    }

    public void OnUpdate()
    {
      if (_controllers == null || _controllers.Length < 2) return;

      var entries = _hotkeys.Select((entry, i) => (
        i,
        entry.Key,
        entry.Value.Item1,
        entry.Value.Item2
      )).ToArray();
      foreach (var (i, hotkey, feature, isDown) in entries)
      {
        if (SpeedrunTools.s_isLegitRunActive && !feature.isAllowedInLegitRuns) continue;
        if (hotkey.Predicate(_controllers[0], _controllers[1]))
        {
          if (isDown) continue;
          _hotkeys[hotkey] = (feature, true);
          hotkey.Handler();
        } else
        {
          _hotkeys[hotkey] = (feature, false);
        }
      }
    }
  }

  public class Hotkey
  {
    public Func<StressLevelZero.Rig.BaseController, StressLevelZero.Rig.BaseController, bool> Predicate { get; set; }
    public Action Handler { get; set; }
  }

  abstract public class Feature
  {
    public bool isAllowedInLegitRuns = false;
    public virtual void OnApplicationStart() { }
    public virtual void OnSceneWasLoaded(int buildIndex, string sceneName) { }
    public virtual void OnSceneWasInitialized(int buildIndex, string sceneName) { }
    public virtual void OnUpdate() { }
  }
}
