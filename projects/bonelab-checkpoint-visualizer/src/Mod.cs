using System.Linq;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Bonelab;
using SLZ.Marrow.Warehouse;
using SLZ.Marrow.SceneStreaming;

namespace Sst {
public class Mod : MelonMod {
  private static Color COLOR_GREEN = new Color(0.2f, 0.8f, 0.2f, 0.5f);
  private static Color COLOR_RED = new Color(0.8f, 0.2f, 0.2f, 0.5f);

  public static Mod Instance;
  public LevelCrate NextLevel;

  private GameControl_KartRace _gameControl;
  private TriggerLasers[] _checkpointTriggers;
  private GameObject[] _renderedColliders;
  private LoadingScene _activeLoadingScene;

  public Mod() { Instance = this; }

  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  public override void OnUpdate() {
    if (_activeLoadingScene != null &&
        !_activeLoadingScene.gameObject.scene.isLoaded) {
      _activeLoadingScene = null;
      if (NextLevel.Title == Utilities.Levels.TITLE_MONOGON_MOTORWAY)
        OnLevelStart();
    }
  }

  public void OnLevelStart() {
    Dbg.Log("OnLevelStart");
    _gameControl = GameObject.FindObjectOfType<GameControl_KartRace>();
    _checkpointTriggers =
        _gameControl.trackCheckPoint
            .Select((_, i) => GameObject.Find($"trigger_{(char)(65 + i)}")
                                  .GetComponent<TriggerLasers>())
            .ToArray();
    _renderedColliders =
        _checkpointTriggers.Select(trigger => RenderTrigger(trigger, false))
            .ToArray();

    foreach (var (trigger, i) in _checkpointTriggers.Select((t, i) => (t, i))) {
      trigger.OnTriggerEnterEvent.AddListener(
          new System.Action<UnityEngine.Collider>((collider) =>
                                                      RerenderTrigger(i)));
    }
    var newLapTrigger =
        GameObject.Find("trigger_newLap").GetComponent<TriggerLasers>();
    newLapTrigger.OnTriggerEnterEvent.AddListener(
        new System.Action<UnityEngine.Collider>(collider =>
                                                    RerenderAllTriggers()));
    RenderTrigger(newLapTrigger, false);
    RenderTrigger(
        GameObject.Find("trigger_start").GetComponent<TriggerLasers>(), false);
  }

  private void RerenderAllTriggers() {
    for (var i = 0; i < _checkpointTriggers.Length; i++)
      RerenderTrigger(i);
  }

  private void RerenderTrigger(int i) {
    Dbg.Log($"RerenderTrigger: {i}");
    if (_renderedColliders[i] != null)
      GameObject.Destroy(_renderedColliders[i]);
    _renderedColliders[i] =
        RenderTrigger(_checkpointTriggers[i], _gameControl.trackCheckPoint[i]);
  }

  private GameObject RenderTrigger(TriggerLasers trigger, bool isTriggered) =>
      Utilities.Collider.Visualize(trigger.cachedCol.gameObject,
                                   trigger.cachedCol,
                                   isTriggered? COLOR_GREEN: COLOR_RED);

  [HarmonyPatch(typeof(SceneStreamer), nameof(SceneStreamer.Load),
                new System.Type[] { typeof(LevelCrateReference),
                                    typeof(LevelCrateReference) })]
  class SceneStreamer_Load_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(LevelCrateReference level) {
      Dbg.Log($"SceneStreamer_Load_Patch: {level.Crate.Title}");
      Mod.Instance.NextLevel = level.Crate;
    }
  }

  [HarmonyPatch(typeof(LoadingScene), nameof(LoadingScene.Start))]
  class LoadingScene_Start_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(LoadingScene __instance) {
      Dbg.Log("LoadingScene_Start_Patch");
      Instance._activeLoadingScene = __instance;
    }
  }
}
}
