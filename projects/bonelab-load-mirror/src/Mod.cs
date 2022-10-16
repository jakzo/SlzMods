using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Data;
using SLZ.Data.SaveData;

namespace Sst {
public class Mod : MelonMod {
  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  [HarmonyPatch(typeof(LoadingScene), nameof(LoadingScene.Start))]
  class LoadingScene_Start_Patch {
    [HarmonyPostfix()]
    internal static void Postfix() {
      Dbg.Log("LoadingScene_Start_Patch");
      var overlay = GameObject.Find("2D_Overlay");
      if (overlay != null) {
        overlay.active = false;
      } else {
        MelonLogger.Warning("2D_Overlay not found in loading screen");
      }

      var spectatorCameraMode =
          DataManager.Instance._settings.SpectatorSettings.SpectatorCameraMode;
      // TODO: != passthrough?
      if (spectatorCameraMode == SpectatorCameraMode.Fisheye) {
      }
    }
  }
}
}
