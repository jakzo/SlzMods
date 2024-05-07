state("BONEWORKS") {
  int levelNumber : "GameAssembly.dll", 0x01E7E4E0, 0xB8, 0x590;
}

startup {
  vars.modLoadPointer = IntPtr.Zero;
  vars.modLoadWatcher = null;
}

init {
  current.split = false;

  var ptr = IntPtr.Zero;
  foreach (var page in game.MemoryPages(true)) {
    var pageScanner =
        new SignatureScanner(game, page.BaseAddress, (int)page.RegionSize);
    ptr = pageScanner.Scan(modLoadPointerTarget);
    if (ptr != IntPtr.Zero)
      break;
  }
  if (ptr == IntPtr.Zero) {
    throw new Exception("Arena state not found - retrying");
  }
  vars.arenaWatcher = new MemoryWatcher<byte>(ptr);
}

update {
  if (vars.modLoadPointer == IntPtr.Zero) {
    var modModule = modules.First(x => x.ModuleName == "SpeedrunTools");
    var scanner = new SignatureScanner(game, modModule.BaseAddress,
                                       modModule.ModuleMemorySize);
    vars.modLoadPointer = scanner.Scan(new SigScanTarget(
        8, "D5 E2 03 34 C2 DF 63 B3 ?? ?? ?? ?? ?? ?? ?? ??"));
    if (vars.modLoadPointer != IntPtr.Zero) {
      print("Mod load pointer address found");
      vars.modLoadWatcher = new MemoryWatcher<byte>(vars.modLoadPointer);
    }
  } else if (vars.modLoadWatcher != null) {
    vars.modLoadWatcher.Update(game);
  }

  vars.levelIsMenu = current.levelNumber <= 1;
  vars.levelWasThroneRoom = old.levelNumber == 15;
  vars.levelChanged = current.levelNumber != old.levelNumber;

  if (vars.levelChanged) {
    vars.nextLevelStopwatch.Restart();
  }

  // Wait 1 second for the fade out before splitting
  current.split = vars.nextLevelStopwatch.Elapsed >= TimeSpan.FromSeconds(1f);

  var isLoadingScene = vars.Helper.Scenes.Active.Name == "loadingScene" ||
                       vars.Helper.Scenes.Active.Name == null;

  if (isLoadingScene && current.split) {
    vars.nextLevelStopwatch.Reset();
  }

  vars.isLoading = current.split || isLoadingScene;
}

isLoading { return vars.isLoading; }

reset {
  return vars.levelChanged && vars.levelIsMenu && !vars.levelWasThroneRoom;
}

split { return current.split && !old.split; }

start { return vars.isLoading && !vars.levelIsMenu; }
