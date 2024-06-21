using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Sst.Utilities;
using HarmonyLib;
using UnityEngine.SceneManagement;

#if ML6
using Il2CppSLZ.Marrow.SceneStreaming;
#else
using SLZ.Marrow.SceneStreaming;
#endif

#if DEBUG

#if ML6
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.Bonelab;
#else
using SLZ.Marrow.Warehouse;
using SLZ.Bonelab;
#endif

using System.Linq;
#endif

namespace Sst.ColliderScope;

public class Mod : MelonMod {
  private enum Mode { NONE, RIG, PHYSICAL, TRIGGER }

  public static Mod Instance;

  private MelonPreferences_Entry<Mode> _prefMode;
  private MelonPreferences_Entry<bool> _prefHideVisuals;
  private MelonPreferences_Entry<float> _prefIterationsPerFrame;
  private MelonPreferences_Entry<float> _prefBackgroundIterationsPerFrame;
  private HashSet<Utilities.Colliders.ColliderVisualization> _visualizations =
      new();
  private HashSet<int> _collidersBeingVisualized = new();
  private HashSet<Renderer> _disabledRenderers = new();
  private HashSet<string> _processedScenes = new();
  private IEnumerator<bool> _visualizerEnumerator = null;
  private bool _isInitializing = false;
  private float _iterationCount = 0f;

  public override void OnInitializeMelon() {
    Instance = this;
    Dbg.Init(BuildInfo.NAME);

    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefMode = category.CreateEntry("mode", Mode.PHYSICAL, "Mode",
                                     "Which things to show colliders of");
    _prefHideVisuals = category.CreateEntry(
        "hideVisuals", false, "Hide visuals",
        "Hides visuals so that only collider visualizations can be seen");
    // TODO: Change to frame time allocated for colliders
    _prefIterationsPerFrame = category.CreateEntry(
        "iterationsPerFrame", 4f, "Iterations per frame",
        "Number of game objects to show colliders of per frame on load (higher loads faster but too high lags and crashes the game)");
    _prefBackgroundIterationsPerFrame = category.CreateEntry(
        "backgroundIterationsPerFrame", 1f, "Background iterations per frame",
        "Number of game objects to show colliders of per frame in the background (runs continuously to catch any objects added during play)");

    LevelHooks.OnLoad += nextLevel => ResetState();
    LevelHooks.OnLevelStart += level => Visualize(false);
    _prefMode.OnEntryValueChanged.Subscribe((a, b) => Visualize(true));
    _prefHideVisuals.OnEntryValueChanged.Subscribe((a, b) => Visualize(true));
  }

  public override void OnUpdate() {
    if (LevelHooks.IsLoading)
      return;

    if (_visualizerEnumerator == null) {
      // Objects can be added to the scene after we've processed everything,
      // so we go through everything again in the background just in case
      Dbg.Log("Iterating again in background...");
      _isInitializing = false;
      _visualizerEnumerator = VisualizeEnumerator(false, false);
    }

    // After the level has loaded, creating all visualizations at once causes
    // a GC crash, so we instead create them a bit at a time
    var iterationsPerFrame = _isInitializing
                                 ? _prefIterationsPerFrame.Value
                                 : _prefBackgroundIterationsPerFrame.Value;
    var startingIterationCount = Mathf.Ceil(_iterationCount);
    _iterationCount += iterationsPerFrame;
    for (var i = startingIterationCount; i < _iterationCount; i += 1f) {
      if (!_visualizerEnumerator.MoveNext()) {
        Dbg.Log("Finished visualizing");
        _visualizerEnumerator = null;
        _iterationCount = 0f;
        break;
      }
    }
  }

  private void ResetState() {
    _visualizations.Clear();
    _collidersBeingVisualized.Clear();
    _disabledRenderers.Clear();
    _processedScenes.Clear();
    _visualizerEnumerator = null;
    _isInitializing = false;
  }

  private void Visualize(bool clear, bool allAtOnce = false) {
    _isInitializing = true;
    if (allAtOnce) {
      Dbg.Log("Visualizing all at once...");
      var enumerator = VisualizeEnumerator(clear, false);
      while (enumerator.MoveNext()) {
      }
    } else {
      Dbg.Log("Starting visualizing...");
      _visualizerEnumerator = VisualizeEnumerator(clear, false);
    }
  }

  private IEnumerator<bool> VisualizeEnumerator(bool clear,
                                                bool onlyUnprocessedScenes) {
    if (clear) {
      foreach (var value in ClearVisualizations()) {
        yield return value;
      }
    }

    var transformsToVisualize = _prefMode.Value == Mode.RIG
                                    ? RigTransforms()
                                    : AllTransforms(onlyUnprocessedScenes);
    var showPhysicalColliders = _prefMode.Value != Mode.TRIGGER;
    foreach (var value in VisualizeTransforms(transformsToVisualize,
                                              showPhysicalColliders)) {
      yield return value;
    }
  }

  private IEnumerable<Transform> RigTransforms() {
    return Utilities.Unity.AllDescendantTransforms(
        LevelHooks.RigManager.transform, true);
  }

  private IEnumerable<Transform> AllTransforms(bool onlyUnprocessedScenes) {
    var scenes = new List<Scene>();
    var sceneCount = SceneManager.sceneCount;
    for (var i = 0; i < sceneCount; i++) {
      scenes.Add(SceneManager.GetSceneAt(i));
    }

    foreach (var scene in scenes) {
      var shouldVisualizeScene =
          scene.isLoaded && scene.IsValid() &&
          (!onlyUnprocessedScenes || !_processedScenes.Contains(scene.name));
      if (!shouldVisualizeScene)
        continue;

      foreach (var rootObject in scene.GetRootGameObjects()) {
        if (!rootObject) // in case scene was unloaded
          continue;
        foreach (var transform in Utilities.Unity.AllDescendantTransforms(
                     rootObject.transform, true)) {
          yield return transform;
        }
      }

      _processedScenes.Add(scene.name);
    }

    if (HaveScenesChanged(scenes)) {
      foreach (var transform in AllTransforms(true)) {
        yield return transform;
      }
    }
  }

  private bool HaveScenesChanged(List<Scene> scenes) {
    var sceneCount = SceneManager.sceneCount;
    if (sceneCount != scenes.Count)
      return true;

    var sceneSet = scenes.Select(scene => scene.name).ToHashSet();
    for (var i = 0; i < sceneCount; i++) {
      sceneSet.Remove(SceneManager.GetSceneAt(i).name);
    }
    return sceneSet.Count > 0;
  }

  private IEnumerable<bool> ClearVisualizations() {
    Dbg.Log("ClearVisualizations");

    _processedScenes.Clear();

    foreach (var visualization in IterateAndRemove(_visualizations)) {
      if (visualization) {
        _collidersBeingVisualized.Remove(
            visualization.Collider.GetInstanceID());

        GameObject.Destroy(visualization.gameObject);

        foreach (var renderer in visualization.transform.parent
                     .GetComponents<Renderer>()) {
          if (_disabledRenderers.Contains(renderer)) {
            renderer.enabled = true;
            _disabledRenderers.Remove(renderer);
          }
        }
        yield return true;
      }
    }
    _visualizations.Clear();
    _collidersBeingVisualized.Clear();

    foreach (var renderer in IterateAndRemove(_disabledRenderers)) {
      if (renderer) {
        renderer.enabled = true;
        yield return true;
      }
    }
    _disabledRenderers.Clear();
  }

  private IEnumerable<T> IterateAndRemove<T>(HashSet<T> set) {
    T value;
    while (set.Count > 0) {
      value = set.First();
      set.Remove(value);
      yield return value;
    }
  }

  public IEnumerable<bool>
  VisualizeTransforms(IEnumerable<Transform> transforms, bool onlyPhysical) {
    foreach (var transform in transforms) {
      if (transform.name.StartsWith("SpeedrunTools_"))
        continue;

      if (onlyPhysical && _prefHideVisuals.Value) {
        foreach (var renderer in transform.GetComponents<Renderer>()) {
          if (renderer) {
            if (!renderer.enabled)
              continue;
            renderer.enabled = false;
            _disabledRenderers.Add(renderer);
          }
        }
      }

      foreach (var uncastedCollider in transform
                   .GetComponents<UnityEngine.Collider>()) {
        var collider = Utilities.Colliders.ToUnderlyingType(uncastedCollider);
        if (_collidersBeingVisualized.Contains(collider.GetInstanceID()) ||
            Utilities.Colliders.IsColliderPhysical(collider) != onlyPhysical)
          continue;

        var color = Utilities.Unity.GenerateColor(
            collider.attachedRigidbody ? collider.gameObject.layer + 2 : 1);

        // Shader default renders double sided but highlighter does not.
        // Mesh colliders only have collision in the direction of their faces
        // but convex mesh colliders are solid.
        if (collider is MeshCollider && !((MeshCollider)collider).convex) {
          _visualizations.Add(Utilities.Colliders.Visualize(
              collider, color, Shaders.HighlightShader));
        } else {
          color.a = 0.05f;
          _visualizations.Add(Utilities.Colliders.Visualize(
              collider, color, Shaders.DefaultShader));
        }
        _collidersBeingVisualized.Add(collider.GetInstanceID());
      }

      yield return true;
    }
  }

  private void OnChunkLoaded() {
    if (!LevelHooks.IsLoading && !_isInitializing) {
      Dbg.Log("Visualizing newly loaded chunk...");
      _isInitializing = true;
      _iterationCount = 0f;
      _visualizerEnumerator = VisualizeEnumerator(false, true);
    }
  }

  [HarmonyPatch(typeof(ChunkTrigger), nameof(ChunkTrigger.Awake))]
  internal static class ChunkTrigger_Awake_Patch {
    [HarmonyPostfix]
    private static void Postfix(ChunkTrigger __instance) {
      __instance.OnChunkLoaded.AddListener(new Action(Instance.OnChunkLoaded));
    }
  }

#if DEBUG
  public override void OnSceneWasInitialized(int buildindex, string sceneName) {
    if (!sceneName.ToUpper().Contains("BOOTSTRAP"))
      return;
    AssetWarehouse.OnReady(new Action(() => {
      var crate = AssetWarehouse.Instance.GetCrates().ToArray().First(
          c => c.Barcode.ID == Levels.Barcodes.HUB);
      var bootstrapper =
          GameObject.FindObjectOfType<SceneBootstrapper_Bonelab>();
      var crateRef = new LevelCrateReference(crate.Barcode.ID);
      bootstrapper.VoidG114CrateRef = crateRef;
      bootstrapper.MenuHollowCrateRef = crateRef;
    }));
  }
#endif
}
