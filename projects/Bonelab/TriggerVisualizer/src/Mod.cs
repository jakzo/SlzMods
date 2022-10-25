using MelonLoader;
using UnityEngine;
using System.Collections.Generic;

namespace Sst {
public class Mod : MelonMod {
  private static Color COLOR_RED = new Color(0.8f, 0.2f, 0.2f);

  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  public override void OnUpdate() {
    if (Input.GetKeyDown(KeyCode.T)) {
      if (GameObject.FindObjectOfType<TriggerVisualization>()) {
        MelonLogger.Msg("Showing trigger visualizations...");
        HideTriggers();
      } else {
        MelonLogger.Msg("Hiding trigger visualizations...");
        ShowTriggers();
      }
    }
  }

  private void ShowTriggers() {
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
      var visualization =
          Utilities.Collider.Visualize(trigger.gameObject, collider, COLOR_RED,
                                       Utilities.Bonelab.HighlightShader);
      visualization.AddComponent<TriggerVisualization>();
    }
  }

  private void HideTriggers() {
    foreach (var visualization in GameObject
                 .FindObjectsOfType<TriggerVisualization>())
      GameObject.Destroy(visualization.gameObject);
  }

  class TriggerVisualization : MonoBehaviour {}

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
  //     Utilities.Collider.Visualize(__instance.gameObject, collider,
  //     COLOR_RED,
  //                                  Utilities.Bonelab.HighlightShader);
  //   }
  // }
}
}
