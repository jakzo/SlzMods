using System;
using MelonLoader;
using UnityEngine;
using Sst;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.Bonelab;
using System.Linq;
using Sst.Utilities;

namespace Jakzo.Testing;

public class Mod : MelonMod {
  public static string STARTING_LEVEL = Levels.Barcodes.HUB;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME, LoggerInstance);
    Features.Init();
    // HotReload.Init();
  }

  public override void OnSceneWasInitialized(int buildindex, string sceneName) {
    if (!sceneName.ToUpper().Contains("BOOTSTRAP") || STARTING_LEVEL == null)
      return;

    AssetWarehouse.OnReady(new Action(() => {
      var crate = AssetWarehouse.Instance.GetCrates().ToArray().First(
          c => c.Barcode.ID == STARTING_LEVEL
      );
      var bootstrapper =
          GameObject.FindObjectOfType<SceneBootstrapper_Bonelab>();
      var crateRef = new LevelCrateReference(crate.Barcode.ID);
      bootstrapper.VoidG114CrateRef = crateRef;
      bootstrapper.MenuHollowCrateRef = crateRef;
    }));
  }
}
