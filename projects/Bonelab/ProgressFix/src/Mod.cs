using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using SLZ.Bonelab;
using SLZ.Marrow.Warehouse;

namespace Sst.ProgressFix {
public class Mod : MelonMod {
  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Utilities.LevelHooks.OnLevelStart += FixTimeTrialLevelSaving;
    FixUnlockCrates();
  }

  // Most time trial levels have a BaseGameController.onSessionEnd listener
  // which calls LevelCompletion.SaveWrite() but this is missing from some
  private static HashSet<string> _brokenTimeTrialLevelIds =
      new HashSet<string>() {
        Utilities.Levels.Barcodes.ROOFTOPS,
      };
  private void FixTimeTrialLevelSaving(LevelCrate level) {
    if (!_brokenTimeTrialLevelIds.Contains(level.Barcode.ID))
      return;
    var controller = GameObject.FindObjectOfType<TimeTrial_GameController>();
    if (!controller)
      return;
    var levelCompletion = new LevelCompletion();
    levelCompletion._LevelKey_k__BackingField = level.Title;
    controller.onSessionEnd.AddListener(new Action(levelCompletion.SaveWrite));
  }

  // There are a several unlocks which seem to not be collectible anywhere in
  // the game
  private static HashSet<string> _missingUnlockableIds = new HashSet<string>() {
    // PolyDebugger
    "SLZ.BONELAB.NoBuild.Avatar.PolyDebugger",
    // Skeleton Pirate
    "SLZ.BONELAB.Content.Avatar.SkeletonPirate",
    // Omni Way
    "fa534c5a868247138f50c62e424c4144.Spawnable.OmniWay",
    // Pallet Jack
    "c1534c5a-52b6-490b-8c20-1cfe50616c6c",
    // MK18 Holo Foregrip
    "SLZ.BONELAB.Content.Spawnable.RifleMK18HoloForegrip",
    // MK18 Holosight
    "c1534c5a-c061-4c5c-a5e2-3d955269666c",
    // MK18 Laser Foregrip
    "c1534c5a-ec8e-418a-a545-cf955269666c",
    // Spear
    "c1534c5a-a97f-4bff-b512-e44d53706561",
    // Ford VR Junkie
    "c1534c5a-481a-45d8-8bc1-d810466f7264",
    // Crate Wooden Destructable
    "c1534c5a-5be2-49d6-884e-d35c576f6f64",
    // Mirror
    "c1534c5a-8fc2-4596-b868-a7644d697272",
    // Monkey
    "c1534c5a-202f-43f8-9a6c-1e9450726f70",
    // Pizzabox
    "SLZ.BONELAB.Content.Spawnable.Pizzabox",
    // Stationary Turret
    "SLZ.BONELAB.Content.Spawnable.PropStationaryTurret",
  };
  private void FixUnlockCrates() {
    foreach (var id in _missingUnlockableIds) {
      var crate = AssetWarehouse.Instance.GetCrate(new Barcode(id));
      if (!crate)
        continue;
      crate.Unlockable = false;
    }
  }
  // Alternate fix in case setting Unlockable to false impacts the game somehow
  // [HarmonyPatch(typeof(MenuProgressControl),
  //               nameof(MenuProgressControl.CalcUnlocks))]
  // class MenuProgressControl_CalcUnlocks_Patch {
  //   [HarmonyPostfix()]
  //   internal static bool Prefix(ref float __result) {
  //     float unlockableFixed =
  //         AssetWarehouseExtensions
  //             .Filter(AssetWarehouse.Instance.GetCrates(),
  //                     new CrateFilters.UnlockableAndNotRedactedCrateFilter()
  //                         .Cast<ICrateFilter<Crate>>())
  //             .ToArray()
  //             .Where(crate =>
  //             !_missingUnlockableIds.Contains(crate.Barcode.ID)) .Count();
  //     float unlocked =
  //         AssetWarehouseExtensions
  //             .Filter(AssetWarehouse.Instance.GetCrates(),
  //                     new CrateFilters.UnlockedAndNotRedactedCrateFilter()
  //                         .Cast<ICrateFilter<Crate>>())
  //             .Count;
  //     __result = unlocked / unlockableFixed;
  //     return false;
  //   }
  // }
}
}
