// Requires the SpeedrunTimer mod to be running
state("BONELAB_Steam_Windows64") {}
state("BONELAB_Oculus_Windows64") {}

startup {
  vars.Log = (Action<object>)(output => print("[BonelabHundredPercent ASL] " + output));

  // See projects/bonelab-timer/src/Utilities/Levels.cs for the mapping of level titles to indexes
  vars.levelIndexDescent = 1;
}

init {
  var initIpc = () => {};
  vars.isLoading = false;
  vars.isSittingInTaxi = false;
  vars.nextLevelIdx = 0;
  vars.watcher = null;
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
  var gotOutOfTaxi = settings["splitAtTaxi"] && vars.prevSittingInTaxi && !vars.isSittingInTaxi;
  var isUnknownLevel = vars.nextLevelIdx != vars.prevLevelIdx && vars.nextLevelIdx == 0;
  return gotOutOfTaxi || isUnknownLevel;
}

onStart {
  timer.IsGameTimePaused = true;
}

exit {
  timer.IsGameTimePaused = true;
}
