using System;
using System.Collections;
using System.Diagnostics;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using SLZ.Marrow.Warehouse;
using SLZ.Marrow.SceneStreaming;
using SLZ.Bonelab;
using SLZ.Utilities;

namespace Sst.SpeedrunPractice.Features {
class Checkpoint : Feature {
  private const float CHECKPOINT_SIZE = 15f;
  private const float CHECKPOINT_DEPTH = 5f;

  private static Stopwatch _stopwatch = new Stopwatch();
  private static PlayerState? _resetState;
  private static TextMeshPro _timeDisplay;
  private static Transform _checkpoint;
  private static PlayerState? _startState;
  private static PlayerState? _fastestEndState;
  private static TimeSpan? _fastestTime;

  public Checkpoint() {
    IsEnabledByDefault = false;

    // Set checkpoint
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Utils.State.rigManager != null &&
                              !Utilities.Levels.IsMenu(
                                  Utils.State.currentLevel?.Barcode.ID ?? "") &&
                              cl.GetBButton() && cr.GetBButton(),
      Handler =
          () => {
            var state = PlayerState.Read();
            MelonLogger.Msg($"Setting checkpoint at: {state.pos.ToString()}");
            _startState = _fastestEndState ?? state;
            _fastestEndState = state;
            if (!_checkpoint) {
              var go = new GameObject("SpeedrunPractice_Checkpoint");
              go.layer = LayerMask.NameToLayer("Trigger");
              var collider = go.AddComponent<BoxCollider>();
              collider.isTrigger = true;
              collider.size = new Vector3(CHECKPOINT_SIZE, CHECKPOINT_SIZE,
                                          CHECKPOINT_DEPTH);
              collider.center = new Vector3(0f, 0f, CHECKPOINT_DEPTH / 2f);
              var trigger = go.AddComponent<TriggerLasers>();
              trigger.LayerFilter =
                  LayerMask.GetMask(new string[] { "Trigger" });
              trigger.onlyTriggerOnPlayer = true;
              trigger.OnTriggerEnterEvent = new UnityEventTrigger();
              trigger.OnTriggerEnterEvent.AddCall(
                  UnityEvent.GetDelegate(new Action(OnCheckpointEnter)));
              _checkpoint = go.transform;
            }
            if (!_timeDisplay) {
              var go = new GameObject("SpeedrunPractice_TimeDisplay");
              go.active = false;
              _timeDisplay = go.AddComponent<TextMeshPro>();
              _timeDisplay.alignment = TextAlignmentOptions.BottomRight;
              _timeDisplay.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
              _timeDisplay.fontSize = 0.2f;
              Utilities.Bonelab.DockToWrist(go);
            }
            _checkpoint.position = state.headPos;
            _checkpoint.rotation = state.headRot;
            _stopwatch.Stop();
            _fastestTime = null;
          },
    });

    // Teleport to set position
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Utils.State.rigManager != null &&
                              !Utilities.Levels.IsMenu(
                                  Utils.State.currentLevel?.Barcode.ID ?? "") &&
                              _startState.HasValue && cr.GetThumbStick(),
      Handler =
          () => {
            MelonLogger.Msg("Teleporting to checkpoint");
            PlayerState.Apply(_startState.Value);
            _timeDisplay?.gameObject.SetActive(false);
            if (_checkpoint)
              _stopwatch.Restart();
          },
    });

    // Reset checkpoints
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => cr.GetAButton() && cr.GetBButton(),
      Handler =
          () => {
            MelonLogger.Msg("Resetting checkpoints");
            ResetCheckpoints();
          },
    });

    // Reset level
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => !Utilities.Levels.IsMenu(
                                  Utils.State.currentLevel?.Barcode.ID ?? "") &&
                              cl.GetAButton() && cl.GetBButton(),
      Handler =
          () => {
            MelonLogger.Msg("Resetting level");
            _resetState = PlayerState.Read();
            SceneStreamer.Reload();
          },
    });
  }

  private void ResetCheckpoints() {
    _startState = null;
    _fastestEndState = null;
    _fastestTime = null;
    if (_checkpoint?.gameObject)
      GameObject.Destroy(_checkpoint.gameObject);
    _checkpoint = null;
    if (_timeDisplay?.gameObject)
      GameObject.Destroy(_timeDisplay.gameObject);
    _timeDisplay = null;
  }

  public override void OnLoadingScreen(LevelCrate nextLevel,
                                       LevelCrate prevLevel) {
    if (nextLevel.Barcode.ID != prevLevel.Barcode.ID || !_resetState.HasValue)
      ResetCheckpoints();
  }

  public override void OnLevelStart(LevelCrate level) {
    if (_resetState.HasValue) {
      Dbg.Log($"Teleporting on reset to: {_resetState.Value.pos.ToString()}");
      MelonCoroutines.Start(RestorePlayerState());
    }
  }

  public void OnCheckpointEnter() {
    if (!_stopwatch.IsRunning)
      return;
    _stopwatch.Stop();
    var elapsed = _stopwatch.Elapsed;
    _timeDisplay.text = elapsed.ToString("m\\:ss\\.ff");
    _timeDisplay.gameObject.active = true;
    MelonLogger.Msg($"Checkpoint reached in: {_timeDisplay.text}");
    if (!_fastestTime.HasValue || elapsed < _fastestTime.Value) {
      MelonLogger.Msg($"Beat checkpoint PB");
      _fastestTime = elapsed;
      _fastestEndState = PlayerState.Read();
      _timeDisplay.color = new Color(0.2f, 0.8f, 0.2f);
    } else {
      _timeDisplay.color = new Color(0.4f, 0.4f, 0.4f);
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
