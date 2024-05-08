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

  vars.LEVEL_MAIN_MENU = 1;
  vars.LEVEL_THRONE_ROOM = 15;

  // === LOADING SCANNER ===
  {
    const int MEGABYTE = 1024 * 1024;
    const int VALUE_SIZE = sizeof(int);
    const string VRCLIENT64_MODULE_NAME = "vrclient_x64.dll";
    const int LOADING_SCENE_INDEX = 25;
    const int LOADING_MARGIN_MS = 100;

    ProcessModule VrclientModule = null;
    HashSet<IntPtr> PossibleAddresses = null;
    bool IsLoading = false;

    Func<Process, string, ProcessModule> GetModule = (proc, name) => {
      foreach (ProcessModule module in proc.Modules) {
        if (module.ModuleName == name) {
          return module;
        }
      }
      throw new Exception("Could not find module: " + name);
    };

    Action<IntPtr> OnLoadingAddressFound = address => {
      vars.isLoading = new MemoryWatcher<bool>(address);
      print("Found loading address: " + address.ToString("X"));
    };

    Action<Process> Scan = proc => {
      var startTime = DateTime.Now;
      var targetValue = IsLoading ? 1 : 0;
      print("Scan(" + targetValue + ")");

      var filteredAddresses = new HashSet<IntPtr>();

      byte[] buffer;
      proc.ReadBytes(VrclientModule.BaseAddress,
                     VrclientModule.ModuleMemorySize, out buffer);

      Action<IntPtr> readFromAddress = address => {
        var value = buffer[(long)address - (long)VrclientModule.BaseAddress];
        if (value == targetValue) {
          filteredAddresses.Add(address);
        }
      };

      if (PossibleAddresses == null) {
        for (var i = 0; i <= buffer.Length - VALUE_SIZE; i += VALUE_SIZE) {
          readFromAddress(VrclientModule.BaseAddress + i);
        }
      } else {
        foreach (var address in PossibleAddresses) {
          readFromAddress(address);
        }
      }

      var duration = (DateTime.Now - startTime).ToString();
      if (PossibleAddresses == null) {
        var totalMb = VrclientModule.ModuleMemorySize / MEGABYTE;
        print("Initial scan found " + filteredAddresses.Count +
              " possible addresses out of " + totalMb + "mb in " + duration +
              "s");
      } else {
        print("Filtered down to " + filteredAddresses.Count +
              " possible addresses from " + PossibleAddresses.Count + " in " +
              duration + "s");
      }

      PossibleAddresses = filteredAddresses;
      if (PossibleAddresses.Count == 1) {
        OnLoadingAddressFound(PossibleAddresses.First());
      } else if (PossibleAddresses.Count == 0) {
        MessageBox.Show("Failed to find loading address.",
                        "LiveSplit | Autosplitter Error", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
      }
    };

    Action<Process> OnUpdate = proc => {
      if (vars.Helper.Loaded &&
          (PossibleAddresses == null || PossibleAddresses.Count > 1)) {
        var sceneIndex = vars.Helper.Scenes.Active.Index;
        var isNowLoading = sceneIndex == LOADING_SCENE_INDEX;
        if (sceneIndex >= 0 && isNowLoading != IsLoading) {
          IsLoading = isNowLoading;

          if (PossibleAddresses != null || IsLoading) {
            if (PossibleAddresses == null) {
              VrclientModule = GetModule(proc, VRCLIENT64_MODULE_NAME);
            }

            var delay = new System.Timers.Timer() {
              Interval = LOADING_MARGIN_MS,
              AutoReset = false,
              Enabled = true,
            };
            delay.Elapsed += (source, args) => {
              delay.Dispose();
              Scan(proc);
            };
          }
        }
      }
    };
    vars.ScannerOnUpdate = OnUpdate;

    Action Reset = () => {
      VrclientModule = null;
      IsLoading = false;
      PossibleAddresses = null;
    };
    vars.ScannerReset = Reset;
  }
}

init {
  vars.isLoading = null;
  vars.isNextLevel = false;
  vars.hasAlreadySplit = false;
}

update {
  vars.ScannerOnUpdate(game);
  if (vars.isLoading != null) {
    vars.isLoading.Update(game);
  }
}

isLoading { return vars.isLoading != null && vars.isLoading.Current; }

start {
  return vars.isLoading != null && current.levelNumber > vars.LEVEL_MAIN_MENU &&
         vars.isLoading.Current;
}

split {
  if (vars.isLoading == null)
    return false;

  if (current.levelNumber > old.levelNumber) {
    vars.isNextLevel = true;
  }

  if (current.levelNumber == vars.LEVEL_MAIN_MENU &&
      old.levelNumber == vars.LEVEL_THRONE_ROOM && vars.isLoading.Current) {
    return true;
  }

  if (vars.isNextLevel && !vars.hasAlreadySplit && vars.isLoading.Current) {
    vars.hasAlreadySplit = true;
    return true;
  }

  if (vars.hasAlreadySplit && vars.isLoading.Current) {
    vars.hasAlreadySplit = false;
    vars.isNextLevel = false;
  }

  return false;
}

reset {
  // Does not work in Throne Room
  return current.levelNumber < old.levelNumber &&
         old.levelNumber != vars.LEVEL_THRONE_ROOM;
}

exit { vars.ScannerReset(); }
