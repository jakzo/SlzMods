using MelonLoader;
using System;
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
    private Hotkey[] _hotkeys { get; set; }
    private bool[] _isKeyDown;
    private StressLevelZero.Rig.BaseController[] _controllers;

    public Hotkeys(params Hotkey[] hotkeys)
    {
      _hotkeys = hotkeys;
    }

    public void Init()
    {
      _isKeyDown = new bool[_hotkeys.Length];
      var controllerObjects = UnityEngine.Object.FindObjectsOfType<StressLevelZero.Rig.BaseController>();
      _controllers = new string[] { "left", "right" }
        .Select(type => $"Controller ({type})")
        .Select(name => controllerObjects.First(controller => controller.name == name))
        .Where(controller => controller != null)
        .ToArray();
    }

    public void OnUpdate()
    {
      if (_hotkeys == null || _controllers == null || _controllers.Length < 2) return;

      foreach (var (def, i) in _hotkeys.Select((value, i) => (value, i)))
      {
        if (def.Predicate(_controllers[0], _controllers[1]))
        {
          if (_isKeyDown[i]) continue;
          _isKeyDown[i] = true;
          def.Handler();
        } else
        {
          _isKeyDown[i] = false;
        }
      }
    }
  }

  public class Hotkey
  {
    public Func<StressLevelZero.Rig.BaseController, StressLevelZero.Rig.BaseController, bool> Predicate { get; set; }
    public Action Handler { get; set; }
  }

  abstract class Feature
  {
    public virtual void OnSceneWasInitialized(int buildIndex, string sceneName) { }
    public virtual void OnUpdate() { }
  }
}
