// Requires the SpeedrunTimer mod to be running
state("BONELAB_Steam_Windows64") {}
state("BONELAB_Oculus_Windows64") {}


startup {
    vars.Log = (Action<object>)(output => print("[BonelabSpeedrunTimer ASL] " + output));

    // See projects/bonelab-timer/src/Utilities/Levels.cs for the mapping of level titles to indexes
    vars.levelIndexDescent = 1;

    settings.Add(
        "gameTimeMsg",
        true,
        "Ask if game time should be used on startup"
    );
    settings.Add(
        "splitAtTaxi",
        true,
        "Split when sitting in the taxi"
    );
    settings.Add(
        "useSpeedrunTimer",
        true,
        "Split based on the SpeedrunTimer mod in-game (required for now)"
    );
}

init {
    vars.isLoading = false;
    vars.isSittingInTaxi = false;
    vars.nextLevelIdx = 0;
    vars.watcher = null;

    var startTime = DateTime.Now;
    var target = new SigScanTarget(8, "D4 E2 03 34 C2 DF 63 24 ?? ?? ?? ??");

    // Try finding the mod then if not found wait up to 10 seconds since the mod
    // will not have initialized yet if the game was just started
    var attempts = 0;
    do {
        attempts++;
        vars.Log("Searching for SpeedrunTimer mod (attempt " + attempts + ")");
        foreach (var page in game.MemoryPages(true)) {
            var scanner = new SignatureScanner(game, page.BaseAddress, (int)page.RegionSize);
            var ptr = scanner.Scan(target);
            if (ptr != IntPtr.Zero) {
                vars.watcher = new MemoryWatcher<uint>(ptr);
                vars.Log("Pointer: " + ptr.ToString());
                break;
            }
        }

        if (vars.watcher == null) {
            if (attempts >= 2) {
                vars.Log("Could not find SpeedrunTimer mod");
                MessageBox.Show(
                    "Could not find the SpeedrunTimer mod. Please make sure the mod is active then restart LiveSplit. " +
                        "Starting LiveSplit before the game can also sometimes cause it to not be found.", 
                    "LiveSplit | BonelabSpeedrunTimer Auto Splitter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            vars.Log("Could not find SpeedrunTimer mod, retrying...");
            Thread.Sleep(15000 - (DateTime.Now - startTime).Milliseconds);
        }
    } while (vars.watcher == null);

    if (timer.CurrentTimingMethod == TimingMethod.RealTime && settings["gameTimeMsg"]) {
        var response = MessageBox.Show(
            "You are currently comparing against \"real time\" which means you will not be able to " +
                "submit to the leaderboard.\nWould you like to switch to \"game time\"?", 
            "LiveSplit | BonelabSpeedrunTimer Auto Splitter",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );
        if (response == DialogResult.Yes)
            timer.CurrentTimingMethod = TimingMethod.GameTime;
    }
}

update {
    vars.prevLevelIdx = vars.nextLevelIdx;
    vars.prevSittingInTaxi = vars.isSittingInTaxi;

    vars.watcher.Update(game);
    var state = vars.watcher.Current;
    vars.isLoading = (state & (1 << 0)) != 0;
    vars.isSittingInTaxi = (state & (1 << 1)) != 0;
    vars.nextLevelIdx = (state >> 8) & 0xFF;
}

isLoading {
    return vars.isLoading;
}

start {
    return vars.nextLevelIdx != vars.prevLevelIdx && vars.nextLevelIdx == vars.levelIndexDescent;
}

split {
    return vars.nextLevelIdx != vars.prevLevelIdx ||
        settings["splitAtTaxi"] && vars.isSittingInTaxi && !vars.prevSittingInTaxi;
}

reset {
    return settings["splitAtTaxi"] && vars.prevSittingInTaxi && !vars.isSittingInTaxi;
}

onStart {
    timer.IsGameTimePaused = true;
}

exit {
    timer.IsGameTimePaused = true;
}
