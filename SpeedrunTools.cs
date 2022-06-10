using MelonLoader;
using System;
using System.Linq;

namespace SpeedrunTools
{
  public static class BuildInfo
  {
    public const string Name = "SpeedrunTools";
    public const string Author = "jakzo";
    public const string Company = null;
    public const string Version = "1.1.0";
    public const string DownloadLink = "https://boneworks.thunderstore.io/package/jakzo/SpeedrunTools/";
  }

  public class SpeedrunTools : MelonMod
  {
    private const string PREF_CATEGORY = "SpeedrunTools";
    private const string PREF_PRINT_DEBUG_LOGS = "printDebugLogs";
    private const string PREF_REMOVE_BOSS_CLAW_RNG = "removeBossClawRng";
    private const string PREF_BOSS_CLAW_X = "bossClawX";

    private static Hotkeys s_hotkeys = new Hotkeys(
      new HotkeyDefinition()
      {
        Predicate = (cl, cr) => s_rigManager != null && cl.GetBButton() && cr.GetBButton(),
        Handler = () =>
        {
          var pos = GetPlayerPos();
          MelonLogger.Msg($"Setting teleport position: {pos.ToString()}");
          s_teleportPos = pos;
        }
      },
      new HotkeyDefinition()
      {
        Predicate = (cl, cr) => s_rigManager != null && s_teleportPos.HasValue && cr.GetThumbStick(),
        Handler = () =>
        {
          MelonLogger.Msg($"Teleporting");
          s_rigManager.Teleport(s_teleportPos.Value);
        }
      },
      new HotkeyDefinition()
      {
        Predicate = (cl, cr) => cl.GetAButton() && cl.GetBButton(),
        Handler = () =>
        {
          MelonLogger.Msg($"Resetting level");
          s_resetPos = GetPlayerPos();
          s_gameControl.RELOADLEVEL();
        }
      }
    );

    private static StressLevelZero.Rig.RigManager s_rigManager;
    private static GameControl s_gameControl;
    private static UnityEngine.Vector3? s_teleportPos;
    private static UnityEngine.Vector3? s_resetPos;
    private static string lastSceneName;

    private static UnityEngine.Vector3 GetPlayerPos()
    {
      return s_rigManager.ControllerRig.transform.position;
    }

    private static T GetPref<T>(string identifier)
    {
      return MelonPreferences.GetCategory(PREF_CATEGORY).GetEntry<T>(identifier).Value;
    }

    private static void LogDebug(string msg)
    {
      if (GetPref<bool>(PREF_PRINT_DEBUG_LOGS))
        MelonLogger.Msg($"DEBUG: {msg}");
    }

    public override void OnApplicationStart()
    {
      var category = MelonPreferences.CreateCategory(PREF_CATEGORY);
      category.CreateEntry(PREF_PRINT_DEBUG_LOGS, false, "Print debug logs to console");
      category.CreateEntry(PREF_REMOVE_BOSS_CLAW_RNG, true, "Make boss claw always patrol to a single point");
      category.CreateEntry(PREF_BOSS_CLAW_X, 120.0f, "The point the boss claw will always patrol to (should be between -100 and 140, default is 120 near level exit)");
      LogDebug("Preferences loaded");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      if (sceneName == "scene_streets" && GetPref<bool>(PREF_REMOVE_BOSS_CLAW_RNG))
      {
        LogDebug("Init boss claw RNG");
        RemoveBossClawRng();
      }

      LogDebug("Init teleport");
      if (sceneName != lastSceneName)
      {
        lastSceneName = sceneName;
        s_teleportPos = null;
      }
      s_rigManager = UnityEngine.Object.FindObjectOfType<StressLevelZero.Rig.RigManager>();

      LogDebug("Init hotkeys");
      s_hotkeys.Init();

      LogDebug("Init reset");
      s_gameControl = UnityEngine.Object.FindObjectOfType<GameControl>();
      if (s_resetPos.HasValue)
      {
        LogDebug($"Teleporting on reset to: {s_resetPos.Value.ToString()}");
        s_rigManager.Teleport(s_resetPos.Value);
        s_resetPos = null;
      }

      LogDebug("Initialization complete");
    }

    public override void OnUpdate()
    {
      s_hotkeys.OnUpdate();
    }

    private void RemoveBossClawRng()
    {
      var bca = UnityEngine.Object.FindObjectOfType<BossClawAi>();
      if (bca == null)
      {
        MelonLogger.Warning("No boss claw in current scene");
        return;
      }

      // Set home position X to near the level exit instead of the middle
      LogDebug("Setting BossClawAi._homePosition");
      var homePosition = bca._homePosition;
      bca._homePosition = new UnityEngine.Vector3(
        GetPref<float>(PREF_BOSS_CLAW_X),
        homePosition.y,
        homePosition.z
      );
      // Reduce patrol area to a point at the home position
      LogDebug("Setting BossClawAi.patrolXz");
      bca.patrolXz = new UnityEngine.Vector2(0.0f, 0.0f);

      // Color the boss claw so it's obvious that it's been modded
      var cabin = UnityEngine.GameObject.Find("/PLACE_STREETS/boss_CLAW/Physics/cabin");
      if (cabin == null)
      {
        MelonLogger.Warning("No boss claw cabin to color in current scene");
        return;
      }
      LogDebug("Coloring boss claw");
      var newMaterial = new UnityEngine.Material(UnityEngine.Shader.Find("Valve/vr_standard"));
      newMaterial.color = new UnityEngine.Color(0.8f, 0.8f, 0.2f);
      for (int i = 0; i < cabin.transform.childCount; i++)
      {
        var child = cabin.transform.GetChild(i).gameObject;
        if (!child.name.StartsWith("kitbash_plate_heavy_4m4m")) continue;
        LogDebug($"Coloring object: {child.name}");
        child.GetComponent<UnityEngine.MeshRenderer>().SetMaterial(newMaterial);
      }

      MelonLogger.Msg("Boss claw AI updated and colored");
    }
  }

  public class Hotkeys
  {
    private HotkeyDefinition[] _hotkeyDefinitions { get; set; }
    private bool[] _isKeyDown;
    private StressLevelZero.Rig.Controller[] _controllers;

    public Hotkeys(params HotkeyDefinition[] HotkeyDefinitions)
    {
      _hotkeyDefinitions = HotkeyDefinitions;
    }

    public void Init()
    {
      _isKeyDown = new bool[_hotkeyDefinitions.Length];
      var controllerObjects = UnityEngine.Object.FindObjectsOfType<StressLevelZero.Rig.Controller>();
      _controllers = new string[] { "left", "right" }
        .Select(type => $"Controller ({type})")
        .Select(name => controllerObjects.First(controller => controller.name == name))
        .Where(controller => controller != null)
        .ToArray();
    }

    public void OnUpdate()
    {
      if (_controllers == null || _controllers.Length < 2) return;

      foreach (var (def, i) in _hotkeyDefinitions.Select((value, i) => (value, i)))
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

  public class HotkeyDefinition
  {
    public Func<StressLevelZero.Rig.Controller, StressLevelZero.Rig.Controller, bool> Predicate { get; set; }
    public Action Handler { get; set; }
  }
}
