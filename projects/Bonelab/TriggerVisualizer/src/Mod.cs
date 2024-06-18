using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using SLZ.Bonelab;

namespace Sst.TriggerVisualizer {
public class Mod : MelonMod {
  private static Color COLOR_RED = new Color(0.8f, 0.2f, 0.2f);

  private List<Utilities.Colliders.ColliderVisualization> _visualizations;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Utilities.LevelHooks.OnLoad += level => _visualizations = null;
  }

  public override void OnUpdate() {
    if (Input.GetKeyDown(KeyCode.T)) {
      if (_visualizations == null) {
        MelonLogger.Msg("Showing trigger visualizations...");
        ShowTriggers();
      } else {
        MelonLogger.Msg("Hiding trigger visualizations...");
        HideTriggers();
      }
    }
  }

  private void ShowTriggers() {
    _visualizations = new List<Utilities.Colliders.ColliderVisualization>();
    foreach (var trigger in GameObject.FindObjectsOfType<TriggerLasers>()) {
      VisualizeColliders(trigger,
                         trigger.gameObject.GetComponents<BoxCollider>());
      VisualizeColliders(trigger,
                         trigger.gameObject.GetComponents<SphereCollider>());
      VisualizeColliders(trigger,
                         trigger.gameObject.GetComponents<CapsuleCollider>());
    }
  }

  private void VisualizeColliders(TriggerLasers trigger,
                                  IEnumerable<Collider> colliders) {
    foreach (var collider in colliders) {
      Dbg.Log($"TriggerLasers visualized: {trigger.gameObject.name}");
      var visualization = Utilities.Colliders.Visualize(
          collider, COLOR_RED, Utilities.Shaders.HighlightShader);
      _visualizations.Add(visualization);
    }
  }

  private void HideTriggers() {
    foreach (var visualization in _visualizations)
      GameObject.Destroy(visualization.gameObject);
    _visualizations = null;
  }

  // Has an error patching for some reason :'(
  // Could possibly show on chunk load instead?
  // [HarmonyPatch(typeof(TriggerLasers), nameof(TriggerLasers.Awake))]
  // class TriggerLasers_Awake_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix(TriggerLasers __instance) {
  //     Dbg.Log("TriggerLasers_Awake_Patch");
  //     var collider = __instance.gameObject.GetComponent<BoxCollider>();
  //     if (collider == null) {
  //       Dbg.Log(
  //           $"TriggerLasers has no BoxCollider:
  //           {__instance.gameObject.name}");
  //       return;
  //     }
  //     Utilities.Collider.Visualize(collider,
  //     COLOR_RED,
  //                                  Utilities.Bonelab.HighlightShader);
  //   }
  // }
}
}
