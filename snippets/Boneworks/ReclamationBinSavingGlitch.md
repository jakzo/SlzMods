A couple of times I've had the reclamation bin not save after unlocking items:

- When unlocking baton while doing RNG segment after runoff
- When unlocking time tower items while doing everything normally except for not collecting hover junkers module in tower

Turns out the reclamation bin _is not_ coded to save the game after reclaiming an item. The game saving is a byproduct.

- Throwing any item in a reclamation bin calls `ReclamationBin.OnTriggerEnter`
- This method unlocks the item in memory and since there are achievements for unlocking certain items, also calls `ReclamationBin.ACHIEVEMENTUNLOCKS`
- This method checks if an achievement item (several level modules and hover junker ship) or the board gun (maybe it used to be an achievement item?) is unlocked then unlocks that achievement if necessary
- Note that reclamation bins do not check for every achievement item, certain bins will check for certain items (eg. museum bin looks for blank box but most others don't, presumably because you can only unlock the blank box module in museum)
- After possibly unlocking the achievement **it saves the game** by calling `Data_Manager.DATA_SAVE` whether or not the achievement was unlocked just then (the game doesn't check, it just calls `SteamStatsAndAchievements.UnlockAchievement` and leaves it up to Steam to decide whether you unlocked it or already have it and should do nothing)

It seems like the default items reclamation bins will check for are board gun, hover junkers module and hover junkers ship. This is the case for the runoff and time tower reclamation bins which explains why the items didn't save. **It was because I hadn't collected the hover junkers module in tower at the time I was using them.**

You can work around this by manually triggering a game save such as by:

- Opening preferences menu, changing something so the "save settings" button appears, saving settings
- Reaching the finish trigger of the level

Fun fact: the game calls `ACHIEVEMENTUNLOCKS` for every collider which enters the reclamation bin, whether or not it is an unlockable. That is why if throw the valve into the reclamation bin in an ng+ save the game freezes for a bit. Because it is saving the game about 100 times (~6 achievement items \* ~15 colliders).
