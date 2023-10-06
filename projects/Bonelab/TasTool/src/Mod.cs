using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Diagnostics;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
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
    if (elapsedUnity < 2f)
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

  // TODO: If the other play methods are called they will also need to patching
  [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), new Type[] {})]
  class AudioSource_Play_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(AudioSource __instance) {
      if (_modifiedAudioSources.TryGetValue(__instance, out var _))
        return;
      // Trigger setter patch but cancel out effect of getter patch
      __instance.pitch = __instance.pitch * CheatEngineTimescale;
    }
  }

  public static void ToggleBodyLog() {
    var progression = DataManager.Instance._activeSave.Progression;
    var isEnabled = !progression.HasBodyLog;
    progression.BodyLogEnabled = isEnabled;
    progression.HasBodyLog = isEnabled;
  }
}
