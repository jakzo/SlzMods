using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Sst.Utilities;
using HarmonyLib;
using SLZ.Marrow.SceneStreaming;
using SLZ.Marrow.Warehouse;
using System.Linq;
using SLZ.Bonelab;
using UnityEngine.SceneManagement;

namespace Sst.Colliders;

public class Mod : MelonMod {
  private enum Mode { NONE, RIG, PHYSICAL, TRIGGER }

  private static Material[] _visualizationMaterials;

  public static Mod Instance;

  private MelonPreferences_Entry<Mode> _prefMode;
  private MelonPreferences_Entry<bool> _prefHideVisuals;
  private HashSet<Utilities.Colliders.ColliderVisualization> _visualizations =
      new();
  private HashSet<UnityEngine.Collider> _collidersBeingVisualized = new();
  private HashSet<MeshRenderer> _disabledRenderers = new();

  public override void OnInitializeMelon() {
    Instance = this;
    Dbg.Init(BuildInfo.NAME);

    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefMode = category.CreateEntry("mode", Mode.PHYSICAL,
                                     "Which things to show colliders of");
    _prefHideVisuals = category.CreateEntry(
        "hideVisuals", false,
        "Hides visuals so that only collider visualizations can be seen");

    LevelHooks.OnLoad += nextLevel => {
      _visualizations.Clear();
      _collidersBeingVisualized.Clear();
      _disabledRenderers.Clear();
    };
    LevelHooks.OnLevelStart += level => Visualize(true);
    _prefMode.OnEntryValueChanged.Subscribe((a, b) => Visualize(true));
    _prefHideVisuals.OnEntryValueChanged.Subscribe((a, b) => Visualize(true));
  }

  private void Visualize(bool clear) {
    if (clear)
      ClearVisualizations();

    switch (_prefMode.Value) {
    case Mode.RIG:
      VisualizeRig();
      break;
    case Mode.PHYSICAL:
      VisualizePhysical();
      break;
    }
  }

  public void ClearVisualizations() {
    Dbg.Log("ClearVisualizations");
    foreach (var visualization in _visualizations) {
      if (visualization)
        GameObject.Destroy(visualization.gameObject);
    }
    _visualizations.Clear();

    foreach (var renderer in _disabledRenderers) {
      if (renderer) {
        renderer.enabled = true;
        renderer.forceRenderingOff = false;
      }
    }
    _disabledRenderers.Clear();

    _collidersBeingVisualized.Clear();
  }

  public void VisualizeRig() {
    Dbg.Log("VisualizeRig");
    VisualizeAllIn(LevelHooks.RigManager.gameObject);
  }

  public void VisualizePhysical() {
    Dbg.Log("VisualizePhysical");
    foreach (var rootObject in Utilities.Unity.RootObjects())
      VisualizeAllIn(rootObject);
  }

  // TODO: Crashes (GC from unknown thread?) if rerendering too many colliders
  public void VisualizeAllIn(GameObject gameObject) {
    Dbg.Log("VisualizeAllIn");
    foreach (var transform in Utilities.Unity.AllDescendantTransforms(
                 gameObject.transform, true)) {
      if (_prefHideVisuals.Value &&
          !transform.name.StartsWith("SpeedrunTools_")) {
        foreach (var renderer in transform.GetComponents<MeshRenderer>()) {
          if (renderer) {
            if (!renderer.enabled)
              continue;
            renderer.enabled = false;
            renderer.forceRenderingOff = true;
            _disabledRenderers.Add(renderer);
          }
        }
      }

      foreach (var uncastedCollider in transform
                   .GetComponents<UnityEngine.Collider>()) {
        var collider = Utilities.Colliders.ToUnderlyingType(uncastedCollider);
        if (!Utilities.Colliders.IsColliderPhysical(collider))
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
        _collidersBeingVisualized.Add(collider);
      }
    }
  }

  [HarmonyPatch(typeof(ChunkTrigger), nameof(ChunkTrigger.Awake))]
  internal static class ChunkTrigger_Awake_Patch {
    [HarmonyPostfix]
    private static void Postfix(ChunkTrigger __instance) {
      Dbg.Log("ChunkTrigger_Awake_Patch");
      __instance.OnChunkLoaded.AddListener(
          new Action(() => Instance.Visualize(false)));
    }
  }

#if DEBUG && false
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
