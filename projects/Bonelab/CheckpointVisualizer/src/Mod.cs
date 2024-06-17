using System.Linq;
using MelonLoader;
using UnityEngine;
using SLZ.Bonelab;
using SLZ.Marrow.Warehouse;

namespace Sst.CheckpointVisualizer {
public class Mod : MelonMod {
  private static Color COLOR_GREEN = new Color(0.2f, 0.8f, 0.2f, 0.25f);
  private static Color COLOR_RED = new Color(0.8f, 0.2f, 0.2f, 0.25f);

  public static Mod Instance;
  public LevelCrate NextLevel;

  private GameControl_KartRace _gameControl;
  private TriggerLasers[] _checkpointTriggers;
  private UnityEngine.Collider[] _checkpointColliders;
  private GameObject[] _renderedColliders;
  private Shader _shader;
  private float _setupAfter = 0;

  public Mod() { Instance = this; }

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Utilities.LevelHooks.OnLevelStart += OnLevelStart;
  }

  // ---
  public override void OnSceneWasInitialized(int buildindex, string sceneName) {
    if (!sceneName.ToUpper().Contains("BOOTSTRAP"))
      return;
    AssetWarehouse.OnReady(new System.Action(() => {
      var crate = AssetWarehouse.Instance.GetCrates().ToArray().First(
          c => c.Title == Utilities.Levels.TITLE_MONOGON_MOTORWAY);
      var bootstrapper =
          GameObject.FindObjectOfType<SceneBootstrapper_Bonelab>();
      var crateRef = new LevelCrateReference(crate.Barcode.ID);
      bootstrapper.VoidG114CrateRef = crateRef;
      bootstrapper.MenuHollowCrateRef = crateRef;
    }));
  }
  // ---

  public override void OnUpdate() {
    if (_setupAfter != 0 && _setupAfter < Time.time) {
      _setupAfter = 0;
      OnMonogonMotorwayStart();
    }
  }

  private void OnLevelStart(LevelCrate level) {
    if (level.Barcode.ID == Utilities.Levels.Barcodes.MONOGON_MOTORWAY) {
      _setupAfter = Time.time + 2;
      Dbg.Log($"setup scheduled for {_setupAfter}");
    }
  }

  private void OnMonogonMotorwayStart() {
    Dbg.Log("OnLevelStart");
    // TODO: How do we get transparency to work using the color alpha?
    _shader = Utilities.Unity.FindShader("SLZ/Highlighter");
    _gameControl = GameObject.FindObjectOfType<GameControl_KartRace>();
    _checkpointTriggers =
        _gameControl.trackCheckPoint
            .Select((_, i) => GameObject.Find($"trigger_{(char)(65 + i)}")
                                  .GetComponent<TriggerLasers>())
            .ToArray();
    _checkpointColliders =
        _checkpointTriggers
            .Select(trigger => trigger.gameObject.GetComponent<BoxCollider>())
            .ToArray();
    _renderedColliders =
        _checkpointColliders.Select(collider => RenderTrigger(collider, false))
            .ToArray();

    foreach (var (trigger, i) in _checkpointTriggers.Select((t, i) => (t, i))) {
      trigger.OnTriggerEnterEvent.AddListener(
          new System.Action<UnityEngine.Collider>((collider) =>
                                                      RerenderTrigger(i)));
    }

    foreach (var name in new[] { "trigger_newLap", "trigger_start" }) {
      var trigger =
          Utilities.Unity
              .FindDescendantTransform(_gameControl.gameObject.transform, name)
              .gameObject;
      trigger.GetComponent<TriggerLasers>().OnTriggerEnterEvent.AddListener(
          new System.Action<UnityEngine.Collider>(collider =>
                                                      RerenderAllTriggers()));
      RenderTrigger(trigger.GetComponent<BoxCollider>(), false);
    }
    Dbg.Log("Rendered triggers");
  }

  private void RerenderAllTriggers() {
    Dbg.Log("RerenderAllTriggers");
    for (var i = 0; i < _checkpointTriggers.Length; i++)
      RerenderTrigger(i);
  }

  private void RerenderTrigger(int i) {
    Dbg.Log($"RerenderTrigger: {i}");
    if (_renderedColliders[i] != null)
      GameObject.Destroy(_renderedColliders[i]);
    _renderedColliders[i] =
        RenderTrigger(_checkpointColliders[i], _gameControl.trackCheckPoint[i]);
  }

  private GameObject RenderTrigger(Collider collider, bool isTriggered) =>
      Utilities.Colliders.Visualize(collider,
                                    isTriggered ? COLOR_GREEN : COLOR_RED,
                                    Utilities.Shaders.HighlightShader);
}
}
