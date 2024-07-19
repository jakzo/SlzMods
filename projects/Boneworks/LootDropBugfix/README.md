Fixes bug where dropped loot sometimes does not spawn.

# Why does the bug happen?

`LootTableData.GetLootItem` returns uses `UnityEngine.Random.RandomRange(0, 100)` to get a random percentage and returns the loot item at that percentage point (eg. if there are 10 items with 10% chance each, a random number of 42 would return the 4th item). They do this by iterating through each item then doing `if (lower < n && n <= upper) return thisItem;` however this logic will never return any item if the randomly generated number is 0 because `lower` starts at 0 and 0 is not less than 0.

There are a couple of issues and likely misunderstandings of `RandomRange` by whoever wrote the code because:

- `RandomRange(0, 100)` returns an _integer_ not a float
  - The loot table uses floats for item chances in the loot table so a non-integer chance for an item will not make a difference
- `RandomRange(0, 100)` can return 0 but not 100
  - The logic uses `n <= 100` for the last item in the loot table but n will never be 100, meaning the last item has a slightly lower chance than the others
  - Eg. for 10 items with 10% chance each the first item would be returned for n = 1 to 10, second for n = 11 to 20, etc. so 10 values of n each until the last item which only has n = 91 to 99

SLZ finally fixed the loot drop bug in patch 4 of Bonelab but they did this by changing the lower bound of the random number from 0 to 1, so the issues listed above still remain.

# Installation

- Make sure [Melon Loader](https://melonwiki.xyz/#/?id=what-is-melonloader) version 0.5.4 is installed in Boneworks
- Download [the mod from Thunderstore](https://boneworks.thunderstore.io/package/jakzo/LootDropBugfix/) (click on "Manual Download")
- Open the downloaded `.zip` file and extract `Mods/LootDropBugfix.dll` into `BONEWORKS/Mods/LootDropBugfix.dll` which is usually at:
  - Steam: `C:\Program Files (x86)\Steam\steamapps\common\BONEWORKS\BONEWORKS`
  - Oculus: `C:\Program Files\Oculus\Software\Software\stress-level-zero-inc-boneworks`

# Links

- Source code: https://github.com/jakzo/SlzSpeedrunTools/tree/main/projects/Boneworks/LootDropBugfix
- Thunderstore: https://boneworks.thunderstore.io/package/jakzo/LootDropBugfix/
