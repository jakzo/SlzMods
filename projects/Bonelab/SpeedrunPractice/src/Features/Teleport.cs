using System;
using System.Collections;
using MelonLoader;
using SLZ.Marrow.Warehouse;
using SLZ.Marrow.SceneStreaming;

namespace Sst.SpeedrunPractice.Features {
class Teleport : Feature {
  private static PlayerState? _resetState;
  private static PlayerState? _teleportState;

  public Teleport() {
    // Set teleport position
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Utils.State.rigManager != null &&
          !Utilities.Levels.IsMenu(
              Utils.State.currentLevel?.Barcode.ID ?? ""
          ) &&
          cl.GetBButton() && cr.GetBButton(),
      Handler =
          () => {
            var state = PlayerState.Read();
            MelonLogger.Msg(
                $"Setting teleport position at: {state.pos.ToString()}"
            );
            _teleportState = state;
          },
    });

    // Teleport to set position
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Utils.State.rigManager != null &&
          !Utilities.Levels.IsMenu(
              Utils.State.currentLevel?.Barcode.ID ?? ""
          ) &&
          _teleportState.HasValue && cr.GetThumbStick(),
      Handler =
          () => {
            MelonLogger.Msg("Teleporting");
            PlayerState.Apply(_teleportState.Value);
          },
    });

    // Reset level
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => !Utilities.Levels.IsMenu(
                                  Utils.State.currentLevel?.Barcode.ID ?? ""
                              ) &&
          cl.GetAButton() && cl.GetBButton(),
      Handler =
          () => {
            MelonLogger.Msg("Resetting level");
            _resetState = PlayerState.Read();
            SceneStreamer.Reload();
          },
    });
  }

  public override void
  OnLoadingScreen(LevelCrate nextLevel, LevelCrate prevLevel) {
    if (nextLevel.Barcode.ID != prevLevel.Barcode.ID)
      _teleportState = null;
  }

  public override void OnLevelStart(LevelCrate level) {
    if (_resetState.HasValue) {
      Dbg.Log($"Teleporting on reset to: {_resetState.Value.pos.ToString()}");
      MelonCoroutines.Start(RestorePlayerState());
    }
  }

  private IEnumerator RestorePlayerState() {
    var state = _resetState.Value;
    _resetState = null;
    // TODO: Is this still true?
    // Wait a bit after level start otherwise will not move the player
    var framesToWait = 15;
    for (var i = 0; i < framesToWait; i++)
      yield return null;
    PlayerState.Apply(state);
  }
}
}
