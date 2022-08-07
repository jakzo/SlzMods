﻿using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Valve.VR;
using HarmonyLib;

namespace SpeedrunTools.Features {
class Replay : Feature {
  private const int SCENE_IDX_MENU = 1;
  private const int SCENE_IDX_START = 2;
  private const int SCENE_IDX_THRONE = 15;
  private const string MANUAL_REPLAY_FILE_PREFIX = "manual";

  private static readonly string GHOST_FILE_PATH =
      Path.Combine(Utils.DIR, $"ghost.{Replays.Constants.REPLAY_EXTENSION}");

  private static Replays.Recorder s_hotkey_recorder;
  private static Replays.Replay s_hotkey_replay;
  private static Replays.Ghost s_hotkey_ghost;

  public static readonly Pref<bool> PrefHotkeys = new Pref<bool>() {
    Id = "enableRecordingHotkeys",
    Name =
        "Start/stop recording with left B + right B and playback with right thumbstick",
    DefaultValue = false
  };

  public readonly Hotkey HotkeyStart = new Hotkey() {
    Predicate = (cl, cr) =>
        cl.GetBButton() && cr.GetBButton() && PrefHotkeys.Read(),
    Handler =
        () => {
          if (s_hotkey_recorder != null && s_hotkey_recorder.IsRecording) {
            s_hotkey_recorder.Stop();
            MelonLogger.Msg("Recording stopped");
          } else {
            s_hotkey_recorder = new Replays.Recorder(MANUAL_REPLAY_FILE_PREFIX);
            MelonLogger.Msg("Recording started");
          }
        }
  };

  public readonly Hotkey HotkeyPlay = new Hotkey() {
    Predicate = (cl, cr) => cr.GetThumbStick() && PrefHotkeys.Read(),
    Handler =
        () => {
          if (s_hotkey_replay == null) {
            var manualReplayFiles =
                Directory.GetFiles(Utils.REPLAYS_DIR)
                    .Where(filename => filename.StartsWith(
                               $"{MANUAL_REPLAY_FILE_PREFIX}-"))
                    .ToArray();
            if (manualReplayFiles.Length == 0)
              return;
            System.Array.Sort(manualReplayFiles);
            s_hotkey_replay = new Replays.Replay(
                Path.Combine(Utils.REPLAYS_DIR, manualReplayFiles[0]));
          }
          if (s_hotkey_ghost != null &&
              (s_hotkey_ghost.IsPlaying || s_hotkey_ghost.IsFinishedPlaying)) {
            s_hotkey_ghost.Stop();
            s_hotkey_ghost = null;
            s_hotkey_replay = null;
            MelonLogger.Msg("Playback stopped");
          } else {
            s_hotkey_ghost = new Replays.Ghost(s_hotkey_replay);
            s_hotkey_ghost.Start();
            MelonLogger.Msg("Playback started");
          }
        }
  };

  public override void OnUpdate() {
    s_hotkey_recorder?.OnUpdate();
    s_hotkey_ghost?.OnUpdate(
        StressLevelZero.Utilities.BoneworksSceneManager.currentSceneIndex);
  }

  public override void OnLoadingScreen(int nextSceneIdx, int prevSceneIdx) {
    s_hotkey_recorder?.OnLevelEnd(prevSceneIdx);
  }

  public override void OnLevelStart(int sceneIdx) {
    s_hotkey_recorder?.OnLevelStart();
  }
}
}