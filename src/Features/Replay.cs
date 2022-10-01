using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Valve.VR;
using HarmonyLib;

namespace SpeedrunTools.Features {
class Replay : Feature {
  public static List<Replays.Replay> AllReplays = new List<Replays.Replay>();

  private static Replays.Recorder _hotkeyRecorder;
  private static Replays.Replay _hotkeyReplay;
  private static Replays.Ghost _hotkeyGhost;
  private static Replays.Recorder _speedrunRecorder;
  private static List<Replays.Ghost> _runGhosts;
  private static System.Func<Transform, Replays.GhostRig> _createRig = parent =>
      new Replays.BlockRig(parent);

  private enum RunGhostsSetting {
    NONE,
    FASTEST_RUN,
    FASTEST_LEVEL,
    ALL_RUN,
    ALL_LEVEL,
  }
  private readonly Dictionary<string, RunGhostsSetting> _runGhostsSettingMap =
      new Dictionary<string, RunGhostsSetting> {
        ["none"] = RunGhostsSetting.NONE,
        ["fastest-run"] = RunGhostsSetting.FASTEST_RUN,
        ["fastest-level"] = RunGhostsSetting.FASTEST_LEVEL,
        ["all-run"] = RunGhostsSetting.ALL_RUN,
        ["all-level"] = RunGhostsSetting.ALL_LEVEL,
      };
  public readonly Pref<string> PrefRunGhosts = new Pref<string>() {
    Id = "replayRunGhosts",
    Name = "Which replay ghosts to display during run attempts",
    DefaultValue = "none",
  };
  public readonly Pref<bool> PrefGhostsOfUnfinishedRuns = new Pref<bool>() {
    Id = "replayGhostsOfUnfinishedRuns",
    Name = "Show ghosts of unfinished runs",
    DefaultValue = true,
  };
  private RunGhostsSetting GetRunGhostsSetting() {
    RunGhostsSetting setting;
    var value = PrefRunGhosts.Read();
    if (!_runGhostsSettingMap.TryGetValue(value, out setting)) {
      MelonLogger.Warning($"Invalid replayRunGhosts setting: {value}");
      return RunGhostsSetting.NONE;
    }
    return setting;
  }

  public readonly Pref<bool> PrefManualReplayHotkeys = new Pref<bool>() {
    Id = "replayManualHotkeys",
    Name = "Allow starting/stopping/playing a replay manually via hotkeys",
    DefaultValue = false,
  };

  public Replay() {
    IsAllowedInRuns = true;
    IsDev = true;

    // Start recording
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          cl.GetBButton() && cr.GetBButton() && PrefManualReplayHotkeys.Read(),
      Handler =
          () => {
            if (_hotkeyRecorder != null && _hotkeyRecorder.IsRecording) {
              _hotkeyRecorder.Stop(false);
              MelonLogger.Msg("Recording stopped");
            } else {
              _hotkeyRecorder = new Replays.Recorder(Bwr.GameMode.NONE, 15);
              MelonLogger.Msg("Recording started");
            }
          },
    });

    // Play most recent manual replay
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          cr.GetThumbStick() && PrefManualReplayHotkeys.Read(),
      Handler =
          () =>
      {
        if (_hotkeyReplay == null) {
          var manualReplayFiles =
              Directory.GetFiles(Utils.REPLAYS_DIR)
                  .Where(
                      filePath => Path.GetFileName(filePath).StartsWith(
                          $"{Replays.Recorder.FILENAME_PREFIXES[Bwr.GameMode.NONE]}-"))
                  .ToArray();
          if (manualReplayFiles.Length == 0)
            return;
          System.Array.Sort(
              manualReplayFiles.Select(Path.GetFileName).ToArray(),
              manualReplayFiles);
          _hotkeyReplay = new Replays.Replay(manualReplayFiles.Last());
        }
        if (_hotkeyGhost != null &&
            (_hotkeyGhost.IsPlaying || _hotkeyGhost.IsFinishedPlaying)) {
          _hotkeyGhost.Stop();
          _hotkeyGhost = null;
          _hotkeyReplay.Close();
          _hotkeyReplay = null;
          MelonLogger.Msg("Playback stopped");
        } else {
          _hotkeyGhost =
              new Replays.Ghost(_hotkeyReplay, true, new Color(1, 1, 1, 0.5f),
                                parent => new Replays.BlockRig(parent));
          _hotkeyGhost.Start();
          MelonLogger.Msg("Playback started");
        }
      },
    });
  }

  public override void OnApplicationStart() {
    foreach (var filePath in Directory.EnumerateFiles(Utils.REPLAYS_DIR))
      if (Path.GetExtension(filePath) ==
          $".{Replays.Constants.REPLAY_EXTENSION}") {
        try {
          AllReplays.Add(new Replays.Replay(filePath));
        } catch (System.Exception err) {
          MelonLogger.Error(err);
        }
      }
  }

  public override void OnUpdate() {
    _hotkeyRecorder?.OnUpdate();
    _hotkeyGhost?.OnUpdate();
    _speedrunRecorder?.OnUpdate();
    if (_runGhosts != null)
      foreach (var ghost in _runGhosts)
        ghost.OnUpdate();
  }

  public override void OnLoadingScreen(int nextSceneIdx, int prevSceneIdx) {
    _hotkeyRecorder?.OnLevelEnd(prevSceneIdx);
    _hotkeyGhost?.OnLoadingScreen();
    _speedrunRecorder?.OnLevelEnd(prevSceneIdx);
    if (_runGhosts != null)
      foreach (var ghost in _runGhosts)
        ghost.OnLoadingScreen();

    // NOTE: This must run after Speedrun.OnLoadingScreen()
    if (Speedrun.Instance.IsRunComplete && _speedrunRecorder != null) {
      _speedrunRecorder.Stop(true);
      _speedrunRecorder = null;
      MelonLogger.Msg("Stopping recording of run");
    }
  }

  public override void OnLevelStart(int sceneIdx) {
    _hotkeyRecorder?.OnLevelStart();
    _hotkeyGhost?.OnLevelStart();
    _speedrunRecorder?.OnLevelStart();
    if (_runGhosts != null)
      foreach (var ghost in _runGhosts)
        ghost.OnLevelStart();

    // NOTE: This must run after Speedrun.OnLevelStart()
    if (Speedrun.Instance.RunTimer.IsActive) {
      var setting = GetRunGhostsSetting();

      if (_speedrunRecorder == null) {
        _speedrunRecorder =
            new Replays.Recorder(Speedruns.Mode.CurrentMode.replayMode, 15);
        MelonLogger.Msg("Starting recording of run");
      }

      if (_runGhosts == null) {
        var requiredGameMode =
            Speedruns.Mode.CurrentMode.replayMode == Bwr.GameMode.NONE
                ? Bwr.GameMode.SPEEDRUN
                : Speedruns.Mode.CurrentMode.replayMode;
        var replays = AllReplays.Where(replay => replay.Metadata.GameMode ==
                                                 requiredGameMode);
        _runGhosts = new List<Replays.Ghost>();
        switch (setting) {
        case RunGhostsSetting.ALL_RUN:
        case RunGhostsSetting.ALL_LEVEL: {
          var i = 0;
          foreach (var replay in replays) {
            var color = Utilities.Unity.GenerateColor(i);
            color.a = 0.5f;
            _runGhosts.Add(new Replays.Ghost(replay, true, color, _createRig));
            i++;
          }
          break;
        }

        case RunGhostsSetting.FASTEST_RUN: {
          replays = replays.Where(replay => replay.Metadata.Completed)
                        .OrderBy(replay => replay.Metadata.Duration);
          if (replays.Count() > 0) {
            var color = Utilities.Unity.GenerateColor(0);
            color.a = 0.5f;
            _runGhosts.Add(
                new Replays.Ghost(replays.First(), true, color, _createRig));
          }
          break;
        }
        }
      }

      if (setting == RunGhostsSetting.ALL_LEVEL && _runGhosts != null) {
        foreach (var ghost in _runGhosts)
          ghost.PlayFromSceneIndex(sceneIdx);
      }
      if (setting == RunGhostsSetting.FASTEST_LEVEL) {
        if (_runGhosts != null)
          foreach (var ghost in _runGhosts)
            ghost.Stop();
        var replays =
            AllReplays
                .Where(replay => replay.Metadata.GameMode ==
                                 Speedruns.Mode.CurrentMode.replayMode)
                .Select(replay => {
                  Bwr.Level? minLevel = null;
                  for (var i = 0; i < replay.Metadata.LevelsLength; i++) {
                    var level = replay.Metadata.Levels(i).Value;
                    if (level.SceneIndex == sceneIdx && level.Completed &&
                        (!minLevel.HasValue ||
                         level.Duration < minLevel.Value.Duration))
                      minLevel = level;
                  }
                  return (minLevel, replay);
                })
                .Where(item => item.minLevel.HasValue)
                .OrderBy(item => item.minLevel.Value.Duration);
        _runGhosts = new List<Replays.Ghost>();
        if (replays.Count() > 0) {
          var (minLevel, replay) = replays.First();
          var color = Utilities.Unity.GenerateColor(0);
          color.a = 0.5f;
          var ghost = new Replays.Ghost(replay, true, color, _createRig);
          ghost.PlayFromLevel(minLevel.Value);
          _runGhosts.Add(ghost);
        }
      }
    } else if (_speedrunRecorder != null) {
      _speedrunRecorder.Stop(false);
      _speedrunRecorder = null;
      MelonLogger.Msg("Stopping recording of run (unfinished)");

      if (_runGhosts != null) {
        foreach (var ghost in _runGhosts)
          ghost.Stop();
        _runGhosts = null;
      }
    }
  }
}
}
