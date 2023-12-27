state("BONEWORKS") {
  int levelNumber : "GameAssembly.dll", 0x01E7E4E0, 0xB8, 0x590;
}

init {
  var module = modules.First(x => x.ModuleName == "vrclient_x64.dll");

  var scanner =
      new SignatureScanner(game, module.BaseAddress, module.ModuleMemorySize);

  vars.loadingPointer = scanner.Scan(new SigScanTarget(
      3,
      "4889??????????488B??????????48FF????488B??????????488B??4889??????????488D??????????488B??????????48FF??????????????????????????????488B??????????418B") {
    OnFound = (process, scanners, addr) =>
        addr + 0x4 + process.ReadValue<int>(addr)
  });
  if (vars.loadingPointer == IntPtr.Zero) {
    throw new Exception("Game engine not initialized - retrying");
  }

  vars.isLoading =
      new MemoryWatcher<bool>(new DeepPointer(vars.loadingPointer, 0xC54));

  vars.loadStartTime = DateTime.Now;
  vars.hasReset = false;
  vars.prevLevelNumber = 0;
}

update {
  vars.isLoading.Update(game);

  if (vars.isLoading.Current && !vars.isLoading.Old) {
    vars.loadStartTime = DateTime.Now;
  }

  if (current.levelNumber != old.levelNumber) {
    vars.prevLevelNumber = old.levelNumber;
  }
  if (vars.isLoading.Old && !vars.isLoading.Current) {
    vars.hasReset = false;
    vars.prevLevelNumber = 0;
  }
}

isLoading { return vars.isLoading.Current; }

start { return vars.isLoading.Current && current.levelNumber > 1; }

split {
  if (vars.isLoading.Current && vars.prevLevelNumber != 0 &&
      vars.prevLevelNumber != current.levelNumber) {
    vars.prevLevelNumber = 0;
    return true;
  }
  return false;
}

reset {
  if (!vars.isLoading.Current)
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
