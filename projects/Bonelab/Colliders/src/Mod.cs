using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Sst.Utilities;

namespace Sst.Colliders;

public class Mod : MelonMod {
  private enum Mode { NONE, RIG, PHYSICAL, TRIGGER }

  private MelonPreferences_Entry<Mode> _prefMode;
  private MelonPreferences_Entry<bool> _prefHideVisuals;
  private HashSet<Utilities.Colliders.ColliderVisualization> _visualizations =
      new();
  private HashSet<MeshRenderer> _disabledRenderers = new();

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefMode = category.CreateEntry("mode", Mode.PHYSICAL,
                                     "Which things to show colliders of");
    _prefHideVisuals = category.CreateEntry(
        "hideVisuals", false,
        "Hides visuals so that only collider visualizations can be seen");

    LevelHooks.OnLevelStart += level => Visualize();
    _prefMode.OnEntryValueChanged.Subscribe((a, b) => Visualize());
    _prefHideVisuals.OnEntryValueChanged.Subscribe((a, b) => Visualize());
  }

  private void Visualize() {
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
    // TODO: Go through slowly to avoid GC crash
    foreach (var visualization in _visualizations) {
      if (visualization)
        GameObject.Destroy(visualization.gameObject);
    }
    _visualizations.Clear();

    foreach (var renderer in _disabledRenderers) {
      if (renderer)
        renderer.enabled = true;
    }
    _disabledRenderers.Clear();
  }

  public void VisualizeRig() {
    Utilities.Colliders.VisualizeAllIn(LevelHooks.RigManager.gameObject);
  }

  public void VisualizePhysical() {
    if (_prefHideVisuals.Value) {
      foreach (var rootObject in Utilities.Unity.RootObjects()) {
        foreach (var renderer in Utilities.Unity
                     .AllDescendantComponents<MeshRenderer>(
                         rootObject.transform, true)) {
          if (renderer.forceRenderingOff)
            continue;
          renderer.forceRenderingOff = true;
          _disabledRenderers.Add(renderer);
        }
      }
    }

    foreach (var collider in Utilities.Colliders.AllColliders()) {
      if (!Utilities.Colliders.IsColliderPhysical(collider))
        continue;

      var color = Utilities.Unity.GenerateColor(
          collider.attachedRigidbody ? collider.gameObject.layer + 2 : 1);
      color.a = collider.attachedRigidbody ? 0.1f : 0.05f;

      // Shader default renders double sided but highlighter does not.
      // Mesh colliders only have collision in the direction of their faces
      // but convex mesh colliders are solid.
      if (collider is MeshCollider && !((MeshCollider)collider).convex) {
        Utilities.Colliders.Visualize(collider, color, Shaders.HighlightShader);
      } else {
        Utilities.Colliders.Visualize(collider, color, Shaders.DefaultShader);
      }
    }
  }
}
