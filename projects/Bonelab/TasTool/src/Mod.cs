using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using SLZ.SaveData;

namespace Sst.TasTool {
public class Mod : MelonMod {
  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Tas.Initialize();
  }
}
}

// Top level namespace to make calling via UnityExplorer easier
public static class Tas {
  public static float CheatEngineTimescale = 1f;

  public static void Initialize() {
    Tas.SetResolution();
    Sst.Utilities.LevelHooks.OnLevelStart += level =>
        SlowdownAudio(CheatEngineTimescale);
  }

  public static void SetResolution() {
    Screen.SetResolution(3840, 1080, FullScreenMode.Windowed);
  }

  public static void SlowdownAudio(float timescale) {
    for (int i = 0; i < SceneManager.sceneCount; i++) {
      var currentScene = SceneManager.GetSceneAt(i);
      if (!currentScene.isLoaded)
        continue;
      foreach (var rootObj in currentScene.GetRootGameObjects()) {
        var audioSources = new List<AudioSource>();
        Sst.Utilities.Unity.FindDescendantComponentsOfType<AudioSource>(
            ref audioSources, rootObj.transform);
        foreach (var audioSource in audioSources) {
          audioSource.pitch *= timescale;
        }
      }
    }
  }

  public static void ToggleBodyLog() {
    var progression = DataManager.Instance._activeSave.Progression;
    var isEnabled = !progression.HasBodyLog;
    progression.BodyLogEnabled = isEnabled;
    progression.HasBodyLog = isEnabled;
  }
}
