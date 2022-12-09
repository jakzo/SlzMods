#r "../../../../references/Bonelab/Assembly-CSharp.dll"
#r "../bin/CompletionistHelper.dll"

// Arena not completed = TunnelTipper
// Parkour not completed = Rooftops
// Tac trial not completed = District, ThreeGunRange

// Non-unlocked
// PolyDebugger (SLZ.BONELAB.NoBuild.Avatar.PolyDebugger)
// Skeleton Pirate (SLZ.BONELAB.Content.Avatar.SkeletonPirate)
// Omni Way (fa534c5a868247138f50c62e424c4144.Spawnable.OmniWay)
// Pallet Jack (c1534c5a-52b6-490b-8c20-1cfe50616c6c)
// MK18 Holo Foregrip (SLZ.BONELAB.Content.Spawnable.RifleMK18HoloForegrip)
// MK18 Holosight (c1534c5a-c061-4c5c-a5e2-3d955269666c)
// MK18 Laser Foregrip (c1534c5a-ec8e-418a-a545-cf955269666c)
// Spear (c1534c5a-a97f-4bff-b512-e44d53706561)
// Ford VR Junkie (c1534c5a-481a-45d8-8bc1-d810466f7264)
// Crate Wooden Destructable (c1534c5a-5be2-49d6-884e-d35c576f6f64)
// Mirror (c1534c5a-8fc2-4596-b868-a7644d697272)
// Monkey (c1534c5a-202f-43f8-9a6c-1e9450726f70)
// Pizzabox (SLZ.BONELAB.Content.Spawnable.Pizzabox)
// Stationary Turret (SLZ.BONELAB.Content.Spawnable.PropStationaryTurret)

// Missing keycards
// KevinC

Func<float> f = () => {
  var x = new Sst.CompletionistHelper.Progress();
  x.Refresh();
  return x.Experimental;
};
f();

var progression = SLZ.SaveData.DataManager.ActiveSave.Progression;

bool completed;
SLZ.Bonelab.BonelabProgressionHelper.TryGetLevelCompleted(progression,
                                                          "Rooftops",
                                                          out completed);
completed;

SLZ.Bonelab.BonelabGameControl.IsCompleted(progression, "Baseline");

var allUnlocks =
    SLZ.Marrow.Warehouse.AssetWarehouseExtensions
        .Filter(
            SLZ.Marrow.Warehouse.AssetWarehouse.Instance.GetCrates(),
            new SLZ.Bonelab.CrateFilters.UnlockableAndNotRedactedCrateFilter()
                .Cast<SLZ.Marrow.Warehouse
                          .ICrateFilter<SLZ.Marrow.Warehouse.Crate>>())
        .ToArray();
var unlocked =
    SLZ.Marrow.Warehouse.AssetWarehouseExtensions
        .Filter(SLZ.Marrow.Warehouse.AssetWarehouse.Instance.GetCrates(),
                new SLZ.Bonelab.CrateFilters.UnlockedAndNotRedactedCrateFilter()
                    .Cast<SLZ.Marrow.Warehouse
                              .ICrateFilter<SLZ.Marrow.Warehouse.Crate>>())
        .ToArray()
        .Select(crate => crate.Barcode.ID)
        .ToHashSet();
string.Join("\n",
            allUnlocks.Where(crate => !unlocked.Contains(crate.Barcode.ID))
                .Select(crate => $"{crate.Title} ({crate.Barcode.ID})"));

string.Join("\n", progression.LevelState._entries.ToArray().SelectMany(
                      entry => new string[] { entry.key }.Concat(
                          (entry.value?._entries.ToArray().Select(
                               entry2 => $"- {entry2.key}") ??
                           new string[] {}))));
