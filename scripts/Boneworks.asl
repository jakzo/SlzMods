state("BONEWORKS") {
  int levelNumber : "GameAssembly.dll", 0x01E7E4E0, 0xB8, 0x590;
}

startup {
  // Copy this file into your LiveSplit/Components folder if running manually:
  // https://github.com/just-ero/asl-help/raw/main/lib/asl-help
  Assembly.Load(File.ReadAllBytes(@"Components\asl-help"))
      .CreateInstance("Unity");
  vars.Helper.GameName = "BONEWORKS";
  vars.Helper.LoadSceneManager = true;

  vars.Helper.AlertGameTime();
}

init { current.levelHasChanged = false; }

update {
  vars.levelIsMenu = current.levelNumber <= 1;
  vars.levelWasThroneRoom = old.levelNumber == 15;
  vars.levelChanged = current.levelNumber != old.levelNumber;

  if (vars.levelChanged) {
    current.levelHasChanged = true;
  }

  var isLoadingScene = vars.Helper.Scenes.Active.Name == "loadingScene" ||
                       vars.Helper.Scenes.Active.Name == null;

  if (isLoadingScene) {
    current.levelHasChanged = false;
  }

  vars.isLoading = current.levelHasChanged || isLoadingScene;
}

isLoading { return vars.isLoading; }

reset {
  return vars.levelChanged && vars.levelIsMenu && !vars.levelWasThroneRoom;
}

split { return vars.levelChanged; }

start { return vars.isLoading && !vars.levelIsMenu; }
