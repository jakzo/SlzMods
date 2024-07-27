// All possible asset tags
// Ammo
// Arena
// Avatar
// Blade
// Blaster
// Blunt
// Decal
// DevTool
// Experimental
// Fragment
// Gadget
// Gun
// Gym
// Hidden
// Humanoid
// Impact
// Menu
// Mod
// NPC
// Other
// Parkour
// Pistol
// Powered
// Projectile
// Prop
// Redacted
// Rifle
// Sandbox
// Shotgun
// SMG
// Story
// TacTrial
// Toy
// Utility
// Vehicle
// VFX
// Weapon
var allTags = SLZ.Marrow.Warehouse.AssetWarehouse.Instance.AllTags.ToArray();
Array.Sort(allTags);
string.Join("\n", allTags);

// All asset tags of unlocks
// Arena
// Avatar
// Blade
// Blunt
// DevTool
// Experimental
// Gadget
// Gun
// Gym
// Humanoid
// Mod
// NPC
// Parkour
// Pistol
// Prop
// Rifle
// Sandbox
// Shotgun
// SMG
// TacTrial
// Toy
// Vehicle
// Weapon
var unlockTags =
    SLZ.Data.DataManager.Instance._activeSave.Unlocks.Unlocks._entries.ToArray()
        .SelectMany(
            entry => entry.key == null
                ? new string[] {}
                : SLZ.Marrow.Warehouse.AssetWarehouse.Instance
                      .GetCrate(new SLZ.Marrow.Warehouse.Barcode(entry.key))
                      .Tags.ToArray()
        )
        .ToHashSet()
        .ToArray();
Array.Sort(unlockTags);
string.Join("\n", unlockTags);

// Non-unlock asset tags
// Ammo
// Blaster
// Decal
// Fragment
// Hidden
// Impact
// Menu
// Other
// Powered
// Projectile
// Redacted
// Story
// Utility
// VFX
string.Join("\n", allTags.ToHashSet().Except(unlockTags));
