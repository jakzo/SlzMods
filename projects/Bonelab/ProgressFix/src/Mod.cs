using System;
using System.Linq;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using SLZ.Bonelab;
using SLZ.Marrow.Warehouse;
using SLZ.Interaction;

namespace Sst.ProgressFix {
public class Mod : MelonMod {
  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Utilities.LevelHooks.OnLevelStart += FixTimeTrialLevelSaving;
    Utilities.LevelHooks.OnLevelStart += FixTunnelTipperSaving;
    Utilities.LevelHooks.OnLevelStart += FixKevinCCard;
    MelonEvents.OnUpdate.Subscribe(FixUnlockCrates);
  }

  public override void OnUpdate() { FixUnlockCrates(); }

  // Most time trial levels have a BaseGameController.onSessionEnd listener
  // which calls LevelCompletion.SaveWrite() but this is missing from some
  private static Dictionary<string, string> _brokenTimeTrialLevelKeys =
      new Dictionary<string, string>() {
        [Utilities.Levels.Barcodes.ROOFTOPS] = "Rooftops",
        [Utilities.Levels.Barcodes.DISTRICT_TAC_TRIAL] = "District",
        [Utilities.Levels.Barcodes.DROP_PIT] = "ThreeGunRange",
      };
  private void FixTimeTrialLevelSaving(LevelCrate level) {
    if (!_brokenTimeTrialLevelKeys.ContainsKey(level.Barcode.ID))
      return;
    var levelKey = _brokenTimeTrialLevelKeys[level.Barcode.ID];
    var controller = GameObject.FindObjectOfType<BaseGameController>();
    if (!controller)
      return;

    Dbg.Log($"Fixing level saving for: {levelKey}");
    var levelCompletion = new LevelCompletion();
    levelCompletion._LevelKey_k__BackingField = levelKey;
    controller.onSessionBegin.AddListener(
        new Action(levelCompletion.SaveWrite));
  }

  // Tunnel Tipper does not mark itself as completed but it's an endless
  // survival game mode which means I don't know when to mark completion so I
  // just mark it completed as soon as it starts
  private void FixTunnelTipperSaving(LevelCrate level) {
    if (level.Barcode.ID != Utilities.Levels.Barcodes.TUNNEL_TIPPER)
      return;
    var levelKey = "TunnelTipper";
    var controller = GameObject.FindObjectOfType<Arena_GameController>();
    if (!controller)
      return;

    Dbg.Log($"Fixing level saving for: {levelKey}");
    var levelCompletion = new LevelCompletion();
    levelCompletion._LevelKey_k__BackingField = levelKey;
    controller.onPlayerEnter.AddListener(new Action(levelCompletion.SaveWrite));
  }

  // The KevinC card reader in Tuscany is missing the
  // IndependentSaver.UpdateSave() onUnlock action that others have
  private void FixKevinCCard(LevelCrate level) {
    if (level.Barcode.ID != Utilities.Levels.Barcodes.TUSCANY)
      return;
    FixKevinCCardReceiver(
        Resources.FindObjectsOfTypeAll<KeycardReciever>().First(
            kr => kr.transform.parent == null));
  }
  private void FixKevinCCardReceiver(KeycardReciever kr) {
    var saveable = kr.gameObject.AddComponent<Saveable>();
    saveable.UniqueId = saveable.BakedObjectPath = "SLZ.Bonelab.card.KevinC";
    saveable.Data = "locked";
    saveable.HashAlgorithm = "path-override";
    saveable.LastBaked = "2022-12-05T00:00:00";

    var saver = kr.gameObject.AddComponent<IndependentSaver>();
    saver.LevelKey = "SLZ.Bonelab.Keycards";
    saver.Saveable = saveable;

    kr.onUnlock.AddListener(new Action(() => {
      Dbg.Log("onUnlock kevin");
      saveable.Data = "unlocked";
      saver.UpdateSave();
    }));
  }

  // There are a several unlocks which seem to not be collectible anywhere in
  // the game
  private static HashSet<string> _missingUnlockableIds = new HashSet<string>() {
    "SLZ.BONELAB.NoBuild.Avatar.PolyDebugger",            // PolyDebugger
    "SLZ.BONELAB.Content.Avatar.SkeletonPirate",          // Skeleton Pirate
    "fa534c5a868247138f50c62e424c4144.Spawnable.OmniWay", // Omni Way
    "c1534c5a-52b6-490b-8c20-1cfe50616c6c",               // Pallet Jack
    "c1534c5a-c061-4c5c-a5e2-3d955269666c",               // MK18 Holosight
    "c1534c5a-ec8e-418a-a545-cf955269666c",               // MK18 Laser Foregrip
    "c1534c5a-a97f-4bff-b512-e44d53706561",               // Spear
    "c1534c5a-481a-45d8-8bc1-d810466f7264",               // Ford VR Junkie
    "c1534c5a-5be2-49d6-884e-d35c576f6f64", // Crate Wooden Destructable
    "c1534c5a-8fc2-4596-b868-a7644d697272", // Mirror
    "c1534c5a-202f-43f8-9a6c-1e9450726f70", // Monkey
    "SLZ.BONELAB.Content.Spawnable.PropStationaryTurret", // Stationary Turret
  };
  private void FixUnlockCrates() {
    var warehouse = AssetWarehouse.Instance;
    if (warehouse == null)
      return;
    foreach (var id in _missingUnlockableIds) {
      var crate = warehouse.GetCrate(new Barcode(id));
      if (crate)
        crate.Unlockable = false;
    }
    MelonEvents.OnUpdate.Unsubscribe(FixUnlockCrates);
  }
}
}
