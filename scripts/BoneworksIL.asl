state("BONEWORKS") {
  int levelNumber : "GameAssembly.dll", 0x01E7E4E0, 0xB8, 0x590;
}

startup {
  vars.boneworksAslHelper =
      Assembly.Load(File.ReadAllBytes(@"Components\BoneworksAslHelper.dll"))
          .CreateInstance("BoneworksAslHelper");
}

init {
  vars.isLoading = false;
  vars.boneworksAslHelper.Initialize();

  vars.loadStartTime = DateTime.Now;
  vars.hasReset = false;
  vars.prevLevelNumber = 0;
}

update {
  if (vars.boneworksAslHelper == null)
    return false;

  var wasLoading = vars.isLoading;
  vars.isLoading = vars.boneworksAslHelper.IsLoading();

  if (vars.isLoading && !wasLoading) {
    vars.loadStartTime = DateTime.Now;
  }

  if (current.levelNumber != old.levelNumber) {
    vars.prevLevelNumber = old.levelNumber;
  }
  if (wasLoading && !vars.isLoading) {
    vars.hasReset = false;
    vars.prevLevelNumber = 0;
  }
}

isLoading { return vars.isLoading; }

start { return vars.isLoading && current.levelNumber > 1; }

split {
  if (vars.isLoading && vars.prevLevelNumber != 0 &&
      vars.prevLevelNumber != current.levelNumber) {
    vars.prevLevelNumber = 0;
    return true;
  }
  return false;
}

reset {
  if (!vars.isLoading)
    return false;

  if (current.levelNumber <= 1)
    return true;

  if (!vars.hasReset) {
    var loadingElapsed = DateTime.Now - vars.loadStartTime;
    if (loadingElapsed.TotalSeconds >= 3f) {
      vars.hasReset = true;
      return true;
    }
  }

  return false;
}

exit {
  timer.IsGameTimePaused = true;
  vars.boneworksAslHelper.Shutdown();
}
