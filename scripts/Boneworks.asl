// Boneworks Speedrunning Discord Server: https://discord.gg/MW2zUcV2Fv
// Authors: DerkO9, Sychke, jakzo

// Inspiration taken from other autosplitter scripts:
// https://fatalis.pw/livesplit/asl-list/
// https://github.com/CryZe/AHatInTimeAutoSplitter/blob/master/AHatInTime.asl

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

startup {
    settings.Add(
        "settings_gameTimeMsg",
        true,
        "Ask if game time should be used when the game opens"
    );
}

init {
    vars.isWaitingForLoadingScene = false;
    vars.isLoading = false;
    vars.loadAfter = null;
    vars.actionOnLoad = "";
    vars.action = "";
    
    if (timer.CurrentTimingMethod == TimingMethod.RealTime && settings["settings_gameTimeMsg"]) {
        var response = MessageBox.Show(
            "You are currently comparing against \"real time\" which includes loading screens.\n" +
                "Would you like to switch to \"game time\"? (recommended)", 
            "LiveSplit | Boneworks Auto Splitter",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );
        if (response == DialogResult.Yes)
            timer.CurrentTimingMethod = TimingMethod.GameTime;
    }
}

update {
    // TODO: Detect when level reload happens
    if (current.sceneIdx != old.sceneIdx) {
        if (old.sceneIdx != 1) {
            var isNextLevel = current.sceneIdx == old.sceneIdx + 1;
            var isGameFinished = current.sceneIdx == 1 && old.sceneIdx == 15;
            vars.actionOnLoad = isNextLevel || isGameFinished ? "split" : "reset";
        }
        vars.isWaitingForLoadingScene = true;
        vars.loadAfter = DateTime.Now.AddSeconds(0.5f);
    }

    if (vars.loadAfter != null && DateTime.Now >= vars.loadAfter) {
        vars.isLoading = true;
        vars.loadAfter = null;
        vars.action = vars.actionOnLoad;
        vars.actionOnLoad = "";
    }

    // loadingScene becomes active about a second after the loading screen shows in the game
    // We want to set isLoading = false once the active scene changes away from loadingScene
    if (current.activeScene12 == "loadingScene") {
        vars.isWaitingForLoadingScene = false;
        // Make sure this is true (sometimes like throne room scene doesn't change until load screen)
        // TODO: Find a way to split at throne room without losing a second
        vars.isLoading = true;
    } else if (!vars.isWaitingForLoadingScene) {
        vars.isLoading = false;
    }
}

isLoading {
    return vars.isLoading;
}

start {
    // Start at every non-menu load screen (for individual level runs)
    return current.sceneIdx > 1 && vars.isLoading && vars.loadAfter == null;
}

split {
    if (vars.action != "split") return false;
    vars.action = "";
    return true;
}

reset {
    if (vars.action != "reset") return false;
    vars.action = "";
    return true;
}
