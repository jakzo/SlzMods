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

state("BONEWORKS") {}

startup {
    vars.Log = (Action<object>)(output => print("[ASL] " + output));

    Assembly.Load(File.ReadAllBytes("Components\asl-help")).CreateInstance("Unity");
    vars.Helper.GameName = "Boneworks";
    vars.Helper.LoadSceneManager = true;

    vars.Helper.AlertGameTime();

    vars.sceneIdx = 0;
}

isLoading {
    // 25 = build index of loading scene
    return vars.Helper.Scenes.Active.Index == 25;
}

update {
    if (current.sceneIdx != old.sceneIdx) {
        vars.Log($"vars.Helper.Scenes.Active.Index = {vars.Helper.Scenes.Active.Index}");
        vars.Log($"vars.Helper.Scenes.Active.Name = {vars.Helper.Scenes.Active.Name}");
    }

    current.sceneIdx = vars.Helper.Scenes.Active.Index;
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
