Shows game progress and achievements for Boneworks 100% speedruns in LiveSplit.

## Installation

- Download [BoneworksHundredStatus.dll](https://github.com/jakzo/SlzMods/raw/main/projects/LiveSplit/BoneworksHundredStatus/Components/BoneworksHundredStatus.dll) (right-click on the link -> save as...)
- Copy `BoneworksHundredStatus.dll` into `LIVESPLIT/Components/BoneworksHundredStatus.dll` (the `LIVESPLIT` directory depends on where you've installed LiveSplit to)

## Setup

1. Make sure you have the SpeedrunTools v2.4 and LootDropBugfix v2.0.1 mods installed or later versions
1. [OPTIONAL] Record the collectibles for your route
   - Simply play through the levels as you would normally while SpeedrunTools is running and it will save a list of all the things you collect after you finish each level to `BONEWORKS/UserData/SpeedrunTools/collectible_recordings/LEVEL_NAME.txt`
   - Note that playing a level again will cause this file to be overwritten
1. [OPTIONAL] Confirm that you collected everything then copy these files to `BONEWORKS/UserData/SpeedrunTools/collectible_order/LEVEL_NAME.txt`
   - This file will now be used during runs to tell you if you missed something and other stats like amount of ammo
   - Note that you can manually edit the order or add/delete collectibles from this list if you want to make a tweak without recording the whole thing again
   - You can skip this step if you are happy to follow my (jakzo's) route, since SpeedrunTools will copy recordings for this route into this folder if the folder does not already exist
1. Add this LiveSplit component to your layout and it should show the stats while SpeedrunTools is running
   - Layout settings -> Add (+) -> Other -> Boneworks 100% Status
   - You may also want to open a second LiveSplit instance and have this as the only component there so that you can customize your OBS layout more

Be aware that some state may not be correct if you restart LiveSplit or Boneworks mid run

## Configuration

**Show level resets instead of item drop tries** is enabled by default. You can disable it in the component's layout settings. This requires an updated SpeedrunTools mod as well as the component. The setting is saved with your layout; an explicitly saved disabled setting remains disabled.

A reset counts when you attempt an uncollected RNG item in Streets or Runoff, then leave through the level-select menu instead of finishing the level. It does not matter how you entered the level. Each visit counts at most once per attempted item. Streets tracks Baseball and Golf Club separately; Runoff tracks Baton. Returning to the main menu or selecting any level, including the same level, counts as a menu exit. Finishing the level, dying, checkpoint reloads, and menu exits without attempts do not add resets. Attempts do not carry across level loads. An item that drops during the visit does not count as a reset when you leave through the menu. For example, getting Baton on the first try and then quitting Runoff stays at zero resets. In Streets, only attempted items that have not dropped gain a reset.

Before a drop, the display reads `Baseball: 5 resets (7 x 10% per try)`. After the drop, it reads `Baseball: 5 resets (7 tries = 52% total)`. As with the tries display, success means the item dropped from a box. Items already reclaimed in the save are excluded from reset eligibility. The reset count is shown starting at zero, for example `Baseball: 0 resets (7 x 10% per try)`. Reset counts and chance text always use the normal layout text color. Only item names use luck colors: white while pending at zero resets, gold when achieved at zero resets, and green at one reset fading to red at ten or more. At one or more resets the name has the same color before and after collection.

Height and whether to show missing items can still be changed by editing `Height` and `ShowMissingCollectibles` in the layout file.

## How it works

- The order of collectibles are stored in `BONEWORKS/UserData/SpeedrunTools/collectible_order/LEVEL_NAME.txt`
  - You may modify these orderings or record your own orderings (instructions above)
- "Level unlocks" readout
  - The total number of unlocks is derived from the number of entries with type `item` in the ordering file for the current level
  - The count is the number of these items from the ordering file which have been reclaimed
- "Level ammo" readout
  - The total number of ammo pickups is derived from the number of entries with type `ammo_*` in the ordering file for the current level
  - The count starts at 0 when the level starts and is incremented every time ammo is picked up
- "Missing" entries
  - It is expected that you will collect each collectible from the ordering file in the same order they are specified there
  - If you collect something which is not the entry after the previous one in the file, all uncollected entries before the entry you just collected will be marked as "missing" and show up in the LiveSplit component
  - Collecting an item not in the ordering file has no effect on the readout
- The display for RNG item chances will always be visible
  - In both display modes, each item starts with a white cross. It changes to a green tick as soon as the item drops from a box, independently of the item's name color.
  - A "try" is a single breaking of a box which has a chance to drop the item
  - The chance of picking up an item is continuously updated to display the chance the previous box had to drop the item (although in practice it should never change)
  - The total is the chance of getting the item after the current number of attempts
    - Finishing with a lower total is more lucky, higher total is unlucky, 50% is average
    - For example, the golf club has a 2% chance of dropping from each raffle box so if you are very lucky and get it from the first box the total will display 2% but if you are unlucky and it takes breaking 100 boxes before the golf club is dropped then the total will display 87%
- After all RNG items have been collected, a percentage showing the overall luck for the run regarding RNG items will display
  - The displayed overall percentile and per-item total percentages are floored to whole percentages. Per-try percentages keep their existing formatting.
  - Lower percentages mean luckier runs. Each item's attempt count determines its cumulative drop chance. The product of those chances orders runs identically to the previous geometric-mean score.
  - The percentile is calculated directly as `P(other run has a lower score) + 0.5 * P(other run ties your score)`. Outcomes are weighted by independent geometric drop probabilities of 10% for Baseball, 2% for Golf Club, and 10% for Baton. No simulation or interpolated lookup table is used.
  - Half-weighting ties gives an expected score of 50%, with almost equally sized percentile buckets. Scores remain between 0% and 100%. First-try drops for all three items score 0.01%, and third-try Baseball, second-try Golf Club, first-try Baton scores about 0.3613%, before display rounding.
  - The Golf Club probabilities are summed analytically. The other two sums omit less than two trillionths of total probability in their extreme tails. Floating-point tolerance is used to recognize ties. The calculation assumes the standard independent drop chances and caches the last completed result.
