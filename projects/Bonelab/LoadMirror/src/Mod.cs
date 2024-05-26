using MelonLoader;
using UnityEngine;
using UnityEngine.XR;
using SLZ.SaveData;
using Sst.Utilities;
using SLZ.Marrow.Warehouse;
using System;
using System.Linq;

namespace Sst.LoadMirror {
public class Mod : MelonMod {
  private GameObject _overlay;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    LevelHooks.OnLoad += OnLoad;
  }

  void OnLoad(LevelCrate nextLevel) {
    var rootObjects =
        LevelHooks.BasicTrackingRig.gameObject.scene.GetRootGameObjects();
    _overlay = rootObjects.FirstOrDefault(go => go.name == "2D_Overlay") ??
               rootObjects.FirstOrDefault(go => go.name == "Canvas");
    if (_overlay != null) {
      _overlay.SetActive(false);
    } else {
      MelonLogger.Warning("Overlay not found in loading screen");
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
