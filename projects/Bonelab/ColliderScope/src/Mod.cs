using System;
using System.Linq;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Sst.Utilities;
using HarmonyLib;
using UnityEngine.SceneManagement;

#if PATCH4 && ML6
using Il2CppSLZ.Marrow.Zones;
#elif PATCH4 && ML5
using SLZ.Marrow.Zones;
#else
using SLZ.Marrow.SceneStreaming;
#endif

#if DEBUG && ML6
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.Bonelab;
#elif DEBUG && ML5
using SLZ.Marrow.Warehouse;
using SLZ.Bonelab;
#endif

namespace Sst.ColliderScope;

public class Mod : MelonMod {
  public enum Mode { NONE, RIG, PHYSICAL, TRIGGER }

  public static Mod Instance;

  private MelonPreferences_Entry<Mode> _prefMode;
  private MelonPreferences_Entry<bool> _prefHideVisuals;
  private MelonPreferences_Entry<bool> _prefHideHeadColliders;
  private MelonPreferences_Entry<bool> _prefOnlyResizeRigColliders;
  private MelonPreferences_Entry<float> _prefIterationsPerFrame;
  private MelonPreferences_Entry<float> _prefBackgroundIterationsPerFrame;
  private HashSet<Colliders.ColliderVisualization> _visualizations = new();
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
        "hideVisuals", true, "Hide visuals",
        "Hides visuals so that only collider visualizations can be seen");
    _prefHideHeadColliders = category.CreateEntry(
        "hideHeadColliders", true, "Hide head colliders",
        "Hides head colliders so that they do not obscure your vision");
    _prefOnlyResizeRigColliders = category.CreateEntry(
        "onlyResizeRigColliders", true, "Only resize rig colliders",
        "Improves performance by watching for changes to collider size on only the rig");
    // TODO: Change to frame time allocated for colliders
    _prefIterationsPerFrame = category.CreateEntry(
        "iterationsPerFrame", 8f, "Iterations per frame",
        "Number of game objects to show colliders of per frame on load (higher loads faster but too high lags and crashes the game)");
    _prefBackgroundIterationsPerFrame = category.CreateEntry(
        "backgroundIterationsPerFrame", 2f, "Background iterations per frame",
        "Number of game objects to show colliders of per frame in the background (runs continuously to catch any objects added during play)");

    LevelHooks.OnLoad += nextLevel => ResetState();
    LevelHooks.OnLevelStart += level => Visualize(false);
    _prefMode.OnEntryValueChanged.Subscribe((a, b) => Visualize(true));
    _prefHideVisuals.OnEntryValueChanged.Subscribe((a, b) => Visualize(true));
    _prefHideHeadColliders.OnEntryValueChanged.Subscribe((a, b) =>
                                                             Visualize(true));
  }

  public override void OnUpdate() {
    if (LevelHooks.IsLoading)
      return;

#if DEBUG
    if (LevelHooks.RigManager?.ControllerRig.rightController
            .GetThumbStickDown() ??
        false) {
      foreach (var transform in Utilities.Unity.AllDescendantTransforms(
                   LevelHooks.RigManager.physicsRig.m_head, true)) {
        if (transform.name.StartsWith("SpeedrunTools_")) {
          transform.gameObject.SetActive(!transform.gameObject.active);
        }
      }
    }
#endif

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

  public void Visualize(bool clear, bool allAtOnce = false) {
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
    foreach (var value in VisualizeTransforms(transformsToVisualize)) {
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

    foreach (var renderer in IterateAndRemove(_disabledRenderers)) {
      if (renderer) {
        renderer.enabled = true;
        yield return true;
      }
    }
    _disabledRenderers.Clear();

    foreach (var visualization in IterateAndRemove(_visualizations)) {
      if (visualization) {
        _collidersBeingVisualized.Remove(
            visualization.Collider.GetInstanceID());
        GameObject.Destroy(visualization.gameObject);
        yield return true;
      }
    }
    _visualizations.Clear();
    _collidersBeingVisualized.Clear();
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
  VisualizeTransforms(IEnumerable<Transform> transforms) {
    var head = LevelHooks.RigManager.physicsRig.m_head;

    foreach (var transform in transforms) {
      if (_prefHideHeadColliders.Value && transform == head)
        continue;

      if (transform.name.StartsWith("SpeedrunTools_")) {
        transform.GetComponent<Colliders.ColliderVisualization>()
            ?.UpdateVisualization();
        yield return true;
        continue;
      }

      if (OnlyShowPhysicalColliders() && _prefHideVisuals.Value) {
        foreach (var renderer in transform.GetComponents<Renderer>()) {
          if (renderer) {
            if (!renderer.enabled)
              continue;
            renderer.enabled = false;
            _disabledRenderers.Add(renderer);
          }
        }
      }

      foreach (var collider in transform.GetComponents<Collider>()) {
        if (_collidersBeingVisualized.Contains(collider.GetInstanceID()))
          continue;

        var watchForChanges =
            !_prefOnlyResizeRigColliders.Value || IsInRig(transform);
        var visualization =
            Colliders.Visualize(collider, Color.black, Shaders.DefaultShader,
                                watchForChanges, OnVisualizationUpdate);
        _visualizations.Add(visualization);
        _collidersBeingVisualized.Add(collider.GetInstanceID());
      }

      yield return true;
    }
  }

  private bool
  OnVisualizationUpdate(Colliders.ColliderVisualization visualization) {
    var shouldBeVisible =
        visualization.Collider.enabled &&
        Colliders.IsColliderPhysical(visualization.Collider) ==
            OnlyShowPhysicalColliders();

    foreach (var renderer in visualization.Renderers) {
      if (renderer.enabled != shouldBeVisible)
        renderer.enabled = shouldBeVisible;
    }

    if (!shouldBeVisible)
      return false;

    var color = Utilities.Unity.GenerateColor(
        // TODO: Change
        visualization.Collider.attachedRigidbody
            ? visualization.Collider.gameObject.layer + 2
            : 1);
    color.a = 0.05f;

    // Mesh colliders only have collision in the direction of their faces
    // but convex mesh colliders are solid
    var isOneSided = visualization.Collider is MeshCollider &&
                     !((MeshCollider)visualization.Collider).convex;
    // Shader default renders double sided but highlighter does not
    var shader = isOneSided ? Shaders.HighlightShader : Shaders.DefaultShader;

    foreach (var renderer in visualization.Renderers) {
      if (renderer.material.color != color)
        renderer.material.color = color;
      if (renderer.material.shader != shader)
        renderer.material.shader = shader;
    }

    return true;
  }

  private bool OnlyShowPhysicalColliders() => _prefMode.Value != Mode.TRIGGER;

  private bool IsInRig(Transform transform) {
    while (transform) {
      if (transform == LevelHooks.RigManager.transform)
        return true;
      transform = transform.parent;
    }
    return false;
  }

  private void OnChunkLoaded() {
    if (!LevelHooks.IsLoading && !_isInitializing) {
      Dbg.Log("Visualizing newly loaded chunk...");
      _isInitializing = true;
      _iterationCount = 0f;
      _visualizerEnumerator = VisualizeEnumerator(false, true);
    }
  }

#if PATCH4
  [HarmonyPatch(typeof(SceneChunk), nameof(SceneChunk.Awake))]
  internal static class SceneChunk_Awake_Patch {
    [HarmonyPostfix]
    private static void Postfix(SceneChunk __instance) {
      __instance.onChunkLoad += new Action(Instance.OnChunkLoaded);
    }
  }
#else
  [HarmonyPatch(typeof(ChunkTrigger), nameof(ChunkTrigger.Awake))]
  internal static class ChunkTrigger_Awake_Patch {
    [HarmonyPostfix]
    private static void Postfix(ChunkTrigger __instance) {
      __instance.OnChunkLoaded.AddListener(new Action(Instance.OnChunkLoaded));
    }
  }
#endif

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
