using MelonLoader;
using UnityEngine;

namespace Sst.Features {
class Teleport : Feature {
  private static GameControl s_gameControl;
  private static Vector3? s_teleportPos;
  private static Vector3? s_resetPos;
  private static int s_currentSceneIdx;

  public Teleport() {
    // Set position
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Mod.GameState.rigManager != null &&
                              s_currentSceneIdx != Utils.SCENE_MENU_IDX &&
                              cl.GetBButton() && cr.GetBButton(),
      Handler =
          () => {
            var pos = Utils.GetPlayerPos();
            MelonLogger.Msg($"Setting teleport position: {pos.ToString()}");
            s_teleportPos = pos;
          },
    });

    // Teleport to set position
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Mod.GameState.rigManager != null &&
                              s_currentSceneIdx != Utils.SCENE_MENU_IDX &&
                              s_teleportPos.HasValue && cr.GetThumbStick(),
      Handler =
          () => {
            MelonLogger.Msg("Teleporting");
            Mod.GameState.rigManager.Teleport(s_teleportPos.Value);
          },
    });

    // Reset level state
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => s_currentSceneIdx != Utils.SCENE_MENU_IDX &&
                              cl.GetAButton() && cl.GetBButton(),
      Handler =
          () => {
            MelonLogger.Msg("Resetting level");
            s_resetPos = Utils.GetPlayerPos();
            s_gameControl.RELOADLEVEL();
          },
    });
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    // Init teleport
    if (buildIndex != s_currentSceneIdx) {
      s_currentSceneIdx = buildIndex;
      s_teleportPos = null;
    }

    // Init reset
    s_gameControl = Object.FindObjectOfType<GameControl>();
    if (s_resetPos.HasValue) {
      Utils.LogDebug($"Teleporting on reset to: {s_resetPos.Value.ToString()}");
      Mod.GameState.rigManager.Teleport(s_resetPos.Value);
      s_resetPos = null;
    }
  }
}
}
