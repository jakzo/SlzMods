using MelonLoader;
using UnityEngine;
using SLZ.Marrow.Warehouse;
using SLZ.Marrow.SceneStreaming;
using System.Collections;

namespace Sst.SpeedrunPractice.Features {
class Teleport : Feature {
  private static Vector3? s_teleportPos;
  private static Vector3? s_resetPos;

  public Teleport() {
    // Set position
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Utils.State.rigManager != null &&
                              !Utilities.Levels.IsMenu(
                                  Utils.State.currentLevel?.Barcode.ID ?? "") &&
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
      Predicate = (cl, cr) => Utils.State.rigManager != null &&
                              !Utilities.Levels.IsMenu(
                                  Utils.State.currentLevel?.Barcode.ID ?? "") &&
                              s_teleportPos.HasValue && cr.GetThumbStick(),
      Handler =
          () => {
            MelonLogger.Msg("Teleporting");
            Utils.State.rigManager.Teleport(s_teleportPos.Value);
          },
    });

    // Reset level state
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => !Utilities.Levels.IsMenu(
                                  Utils.State.currentLevel?.Barcode.ID ?? "") &&
                              cl.GetAButton() && cl.GetBButton(),
      Handler =
          () => {
            MelonLogger.Msg("Resetting level");
            s_resetPos = Utils.GetPlayerPos();
            SceneStreamer.Reload();
          },
    });
  }

  public override void OnLoadingScreen(LevelCrate nextLevel,
                                       LevelCrate prevLevel) {
    // Reset teleport position after changing levels
    if (nextLevel.Barcode.ID != prevLevel.Barcode.ID)
      s_teleportPos = null;
  }

  public override void OnLevelStart(LevelCrate level) {
    // Init reset
    if (s_resetPos.HasValue) {
      Dbg.Log($"Teleporting on reset to: {s_resetPos.Value.ToString()}");
      MelonCoroutines.Start(TeleportToResetPos());
    }
  }

  private IEnumerator TeleportToResetPos() {
    var resetPos = s_resetPos.Value;
    s_resetPos = null;
    // Wait a bit after level start otherwise teleport will not move the player
    var framesToWait = 15;
    for (var i = 0; i < framesToWait; i++)
      yield return null;
    Utils.State.rigManager.Teleport(resetPos);
  }
}
}
