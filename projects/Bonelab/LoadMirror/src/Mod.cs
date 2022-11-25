using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;
using SLZ.SaveData;
using SLZ.UI;

namespace Sst.LoadMirror {
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

      // Turn XR game rendering back on when loading while spectator cam is set
      // to fisheye (automatically turns back off after loading finishes)
      var spectatorCameraMode =
          DataManager.Instance._settings.SpectatorSettings.SpectatorCameraMode;
      if (spectatorCameraMode == SpectatorCameraMode.Fisheye) {
        Dbg.Log("XRSettings.gameViewRenderMode = GameViewRenderMode.LeftEye");
        XRSettings.gameViewRenderMode = GameViewRenderMode.LeftEye;
      }
    }
  }
}
}
