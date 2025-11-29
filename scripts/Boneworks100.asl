state("BONEWORKS") {
  int levelNumber : "GameAssembly.dll", 0x01E7E4E0, 0xB8, 0x590;
}

startup {
  vars.boneworksAslHelper =
      Assembly.Load(File.ReadAllBytes(@"Components\BoneworksAslHelper.dll"))
          .CreateInstance("BoneworksAslHelper");
}

init {
  vars.boneworksAslHelper.Initialize();

  vars.isLoading = false;
  vars.wasLoading = false;

  var arenaTarget = new SigScanTarget(7, "D5 E2 03 34 C2 DF 63 ??");
  var ptr = IntPtr.Zero;
  foreach (var page in game.MemoryPages(true)) {
    var pageScanner =
        new SignatureScanner(game, page.BaseAddress, (int)page.RegionSize);
    ptr = pageScanner.Scan(arenaTarget);
    if (ptr != IntPtr.Zero)
      break;
  }
  if (ptr == IntPtr.Zero) {
    vars.arenaWatcher = null;
    // Disabling in case old mod version without arena state is being used
    // throw new Exception("Arena state not found - retrying");
  } else {
    vars.arenaWatcher = new MemoryWatcher<byte>(ptr);
  }

  // Index in levelOrder to start the run at (for practice)
  vars.startingSplit = 0;

  // Will split when entering each level in this list in this order
  // If entering a level later in the list it will split until it reaches it
  vars.levelOrder = new int[] {
    2,  // scene_theatrigon_movie01 -> scene_breakroom
    4,  // scene_museum
    5,  // scene_streets
    6,  // scene_runoff
    7,  // scene_sewerStation
    8,  // scene_warehouse
    9,  // scene_subwayStation
    10, // scene_tower
    11, // scene_towerBoss
    12, // scene_theatrigon_movie02 -> scene_dungeon
    14, // scene_arena
    15, // scene_throneRoom
    1,  // scene_mainMenu
    18, // scene_redactedChamber
    19, // sandbox_handgunBox
    22, // scene_hoverJunkers
    1,  // scene_mainMenu
    16, // arena_fantasy
    23, // zombie_warehouse
    1,  // scene_mainMenu
    1,  // scene_mainMenu
    1,  // scene_mainMenu
  };
  vars.levelOrderIdx = 0;
  vars.targetLevelOrderIdx = vars.startingSplit;
}

update {
  if (vars.boneworksAslHelper == null)
    return false;

  vars.wasLoading = vars.isLoading;
  vars.isLoading = vars.boneworksAslHelper.IsLoading();

  if (vars.arenaWatcher != null)
    vars.arenaWatcher.Update(game);
}

isLoading { return vars.isLoading; }

start {
  return vars.isLoading &&
      current.levelNumber == vars.levelOrder[vars.startingSplit];
}

split {
  if (vars.arenaWatcher != null &&
      vars.arenaWatcher.Current != vars.arenaWatcher.Old) {
    return true;
  }

  if (vars.isLoading &&
      (!vars.wasLoading || current.levelNumber != old.levelNumber)) {
    var nextLevelOrderIdx = vars.targetLevelOrderIdx + 1;
    if (nextLevelOrderIdx < vars.levelOrder.Length &&
        vars.levelOrder[nextLevelOrderIdx] == current.levelNumber) {
      vars.targetLevelOrderIdx = nextLevelOrderIdx;
    }
  }

  if (vars.levelOrderIdx < vars.targetLevelOrderIdx) {
    vars.levelOrderIdx++;
    // Skip splitting here because fantasy arena splits at the end
    var isFirstZombieWarehouseSplit =
        vars.levelOrderIdx == Array.IndexOf(vars.levelOrder, 23);
    return !isFirstZombieWarehouseSplit;
  }

  return false;
}

onReset {
  vars.levelOrderIdx = 0;
  vars.targetLevelOrderIdx = vars.startingSplit;
}

exit { timer.IsGameTimePaused = true; }
