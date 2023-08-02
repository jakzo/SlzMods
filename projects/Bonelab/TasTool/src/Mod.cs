using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MelonLoader;
using HarmonyLib;
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
  private static DateTime _measureTimescaleStartRealtime;
  private static double _measureTimescaleStartUnity;
  private static ConditionalWeakTable<AudioSource, AudioSource>
      _modifiedAudioSources =
          new ConditionalWeakTable<AudioSource, AudioSource>();

  public static float CheatEngineTimescale = 1f;

  public static void Initialize() {
    Tas.SetResolution();
    Sst.Utilities.LevelHooks.OnLoad += level => MeasureTimescale();
    // TODO: This works for most sounds but there are still some that are
    // created on the fly (despawn sound) but never have their pitch set
    Sst.Utilities.LevelHooks.OnLevelStart += level =>
        SlowDownAudio(CheatEngineTimescale);
  }

  public static void SetResolution() {
    Screen.SetResolution(3840, 1080, FullScreenMode.Windowed);
  }

  public static void MeasureTimescale() {
    _measureTimescaleStartRealtime = DateTime.Now;
    _measureTimescaleStartUnity = Time.unscaledTimeAsDouble;
    MelonEvents.OnUpdate.Subscribe(MeasureTimescaleOnUpdate);
  }

  private static void MeasureTimescaleOnUpdate() {
    var elapsedUnity = Time.unscaledTimeAsDouble - _measureTimescaleStartUnity;
    if (elapsedUnity < 1f)
      return;
    MelonEvents.OnUpdate.Unsubscribe(MeasureTimescaleOnUpdate);
    var elapsedRealtime =
        (DateTime.Now - _measureTimescaleStartRealtime).TotalSeconds;
    CheatEngineTimescale = (float)(elapsedUnity / elapsedRealtime);
    MelonLogger.Msg(
        $"Time scale measured as: {CheatEngineTimescale.ToString("F2")}");
  }

  [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.pitch),
                MethodType.Setter)]
  class AudioSource_set_pitch_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(ref float value, AudioSource __instance) {
      value *= CheatEngineTimescale;
      _modifiedAudioSources.Add(__instance, __instance);
    }
  }

  [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.pitch),
                MethodType.Getter)]
  class AudioSource_get_pitch_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(ref float __result) {
      __result /= CheatEngineTimescale;
    }
  }

  public static void SlowDownAudio(float timescale) {
    MelonLogger.Msg($"Slowing down audio to: {timescale.ToString("F2")}");
    for (int i = 0; i < SceneManager.sceneCount; i++) {
      var scene = SceneManager.GetSceneAt(i);
      if (!scene.isLoaded)
        continue;
      foreach (var rootObj in scene.GetRootGameObjects()) {
        var audioSources = new List<AudioSource>();
        Sst.Utilities.Unity.FindDescendantComponentsOfType<AudioSource>(
            ref audioSources, rootObj.transform);
        foreach (var audioSource in audioSources) {
          if (_modifiedAudioSources.TryGetValue(audioSource, out var _))
            continue;
          // Trigger setter patch
          audioSource.pitch = audioSource.pitch;
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
