// Requires the SpeedrunTimer mod to be running
state("BONELAB_Steam_Windows64") {}
state("BONELAB_Oculus_Windows64") {}

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

startup {
    vars.Log = (Action<object>)(output => print("[BonelabSpeedrunTimer ASL] " + output));
    vars.isLoading = false;
    vars.nextLevel = "";

    settings.Add(
        "settings_gameTimeMsg",
        true,
        "Ask if game time should be used on startup"
    );
    settings.Add(
        "settings_useSpeedrunTimer",
        true,
        "Split based on the SpeedrunTimer mod in-game"
    );
}

init {
    vars.isLoading = false;
    vars.nextLevel = "";

    var pipe = new System.IO.Pipes.NamedPipeClientStream(
        ".",
        "BonelabSpeedrunTimer",
        System.IO.Pipes.PipeDirection.In
    );
    var reader = new StreamReader(pipe);

    vars.pipeThread = new Thread(() => {
        while (true) {
            try {
                vars.Log("Attempting to connect to pipe...");
                pipe.Connect();
                if (pipe.IsConnected) vars.Log("Pipe connected");
                while (pipe.IsConnected) {
                    try {
                        var line = reader.ReadLine();
                        vars.Log("Pipe: " + line);
                        if (line == null) continue;
                        if (line.StartsWith(":L:")) {
                            vars.isLoading = true;
                            vars.nextLevel = line.Substring(3);
                        } else if (line.StartsWith(":S:")) {
                            vars.isLoading = false;
                            vars.nextLevel = line.Substring(3);
                        } else {
                            vars.Log("Unrecognized message from pipe");
                        }
                    } catch (Exception err) {
                        vars.Log("Error reading from pipe: " + err.Message);
                    }
                }
            } catch (Exception err) {
                vars.Log("Error connecting to pipe: " + err.Message);
            }
            vars.Log("Pipe disconnected");
            Thread.Sleep(1000);
        }
    });
    vars.pipeThread.IsBackground = true;
    vars.pipeThread.Start();
    vars.Log("Started thread");

    if (timer.CurrentTimingMethod == TimingMethod.RealTime && settings["settings_gameTimeMsg"]) {
        var response = MessageBox.Show(
            "You are currently comparing against \"real time\" which includes loading screens.\n" +
                "Would you like to switch to \"game time\"? (recommended)", 
            "LiveSplit | BonelabSpeedrunTimer Auto Splitter",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );
        if (response == DialogResult.Yes)
            timer.CurrentTimingMethod = TimingMethod.GameTime;
    }
}

update {
    
}

isLoading {
    return vars.isLoading;
}

start {
    return vars.nextLevel == "descent";
}

split {
    return false;
}

reset {
    return false;
}
