// Boneworks Speedrunning Discord Server: https://discord.gg/MW2zUcV2Fv
// Authors: DerkO9, Sychke, jakzo

state("BONEWORKS") {
    // sceneIdx = scene name
    //  0 = scene_introStart
    //  1 = scene_mainMenu
    //  2 = scene_theatrigon_movie01
    //  3 = scene_breakroom
    //  4 = scene_museum
    //  5 = scene_streets
    //  6 = scene_runoff
    //  7 = scene_sewerStation
    //  8 = scene_warehouse
    //  9 = scene_subwayStation
    // 10 = scene_tower
    // 11 = scene_towerBoss
    // 12 = scene_theatrigon_movie02
    // 13 = scene_dungeon
    // 14 = scene_arena
    // 15 = scene_throneRoom
    // 16 = arena_fantasy
    // 18 = scene_redactedChamber
    // 19 = sandbox_handgunBox
    // 20 = sandbox_museumBasement
    // 21 = sandbox_blankBox
    // 22 = scene_hoverJunkers
    // 23 = zombie_warehouse
    // 24 = empty_scene
    // 25 = loadingScene

    int sceneIdx : "GameAssembly.dll", 0x01E7E4E0, 0xB8, 0x590;
    // Note that this doesn't contain the scene name for all levels (eg. sewers and central)
    // but should always contain loadingScene when loading
    string12 activeScene12 : "UnityPlayer.dll", 0x0155F9D8, 0x8, 0x0, 0x40;
    // TODO: Find the address of SceneLoader._active because that is 0 when not loading and becomes
    //       non-zero the moment loading starts (will allow us to stop the timer when reloading a level)

    // To find activeScene pointer:
    // Open BONEWORKS.exe process in Cheat Engine
    // Search for "value type" = string, "text" = the name of the current scene according to scene names in list above
    // If you get heaps of results (~100) try changing levels in the game and search again
    // Right click in box at bottom -> generate pointermap -> save as any file
    // For each result in the list, browse memory (CTRL+B) and delete any which are not the scene name surrounded by junk
    //   (eg. delete any which are a list of level names, name within a JSON string, an asset file path, etc.)
    // You should have narrowed it down to one address (if not should be a low amount, just try the rest of the steps with them all and one should work)
    // Add the one found address to the box in bottom (double click) then right click -> pointer scan for this address
    // Tick "use saved pointermap" and select the file saved in the earlier step
    // Set "max level" to 4
    // Click "ok", save as any file, wait until it finds a list of pointers
    // Change levels in game then open the "pointer scanner" menu -> rescan memory
    // Enter the address of the new level name (you should see some pointers in the list which have a new address with the new level)
    // Clicking "ok" should narrow down this list
    // Repeat and do the same thing while the loadingScene is active
    // Close Boneworks then open it again without closing Cheat Engine
    // Reattach the Boneworks process in Cheat Engine and filter the pointer list again
    // You now have a small list of pointers you can use in the Autosplit file 🎉
}

init {
    vars.isSceneIdxChanged = false;
    vars.isLoading = false;
}

update {
    // TODO: Detect when loading actually starts instead of this so it sets
    //       isLoading immediately when reloading the scene
    if (current.sceneIdx != old.sceneIdx) {
        vars.isSceneIdxChanged = true;
        vars.isLoading = true;
    }
    // loadingScene becomes active about a second after the loading screen shows in the game
    if (current.activeScene12 == "loadingScene") {
        vars.isLoading = true;
        vars.isSceneIdxChanged = false;
    } else if (!vars.isSceneIdxChanged) {
        vars.isLoading = false;
    }
}

isLoading {
    return vars.isLoading;
}

start {
    return old.sceneIdx == 1 && current.sceneIdx == 2;
}

split {
    var isNextLevel = current.sceneIdx == old.sceneIdx + 1;
    var isGameFinished = current.sceneIdx == 1 && old.sceneIdx == 15;
    return vars.isLoading && (isNextLevel || isGameFinished);
}

reset {
    var isNotNextLevel = current.sceneIdx != old.sceneIdx && current.sceneIdx != old.sceneIdx + 1;
    var isGameFinished = current.sceneIdx == 1 && old.sceneIdx == 15;
    return vars.isLoading && isNotNextLevel && !isGameFinished;
}
