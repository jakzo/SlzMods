//	Autosplitter created by Jakz0 and Sychke
//	Boneworks Speedrunning Discord Server: https://discord.gg/MW2zUcV2Fv

//	levelNumber is the ID of the current level
// 	Main Menu = 1, CutsceneOne = 2, BreakRoom = 3, Museum = 4, Streets = 5, Runoff = 6, Sewers = 7, Warehouse = 8,
//	Central Station = 9, Tower = 10, Time Tower = 11, CutsceneTwo = 12, Dungeon = 13, Arena = 14, Throne Room = 15

state("BONEWORKS"){	//levelNumber should always be accurate
	int levelNumber : "GameAssembly.dll", 0x01E7E4E0, 0xB8, 0x590;
}

startup {
	vars.modLoadPointer = IntPtr.Zero;
	vars.modLoadWatcher = null;
	vars.isLoading = null;
}

init{
	vars.stillLoading = 0;
	vars.levelNumGreater = 0;
}

update{
	if (vars.modLoadPointer == IntPtr.Zero) {
		foreach (var page in game.MemoryPages(true)) {
			var scanner = new SignatureScanner(game, page.BaseAddress, (int)page.RegionSize);
			vars.modLoadPointer = scanner.Scan(new SigScanTarget(
				8, "D5 E2 03 34 C2 DF 63 B3 ?? ?? ?? ?? ?? ?? ?? ??"));
			if (vars.modLoadPointer != IntPtr.Zero) {
				print("Mod load pointer address found");
				vars.modLoadWatcher = new MemoryWatcher<IntPtr>(vars.modLoadPointer);
				break;
			}
		}
	}

	if (vars.modLoadWatcher != null && vars.isLoading == null) {
		vars.modLoadWatcher.Update(game);
		if (vars.modLoadWatcher.Current != IntPtr.Zero) {
			print("Mod load address found: " + vars.modLoadWatcher.Current.ToString("X"));
			vars.isLoading = new MemoryWatcher<bool>(vars.modLoadWatcher.Current);
		}
	}

	if (vars.isLoading != null) {
		vars.isLoading.Update(game);
	}
}

isLoading{
	if (vars.isLoading == null) return false;
	return vars.isLoading.Current;
}

start{
	if (vars.isLoading == null) return false;
	//Starts if the levelNumber is greater than 1 and isLoading is true
	if (current.levelNumber > 1){
		return vars.isLoading.Current;
	}
}

split{
	if (vars.isLoading == null) return false;
	//Checks if the new levelNumber is greater than the old levelNumber
	if (current.levelNumber > old.levelNumber){
		vars.levelNumGreater = 1;
	}
	//If you are in throne room and 
	if (current.levelNumber == 1 && old.levelNumber == 15 && vars.isLoading.Current){
		return true;
	}
	//When the new levelNumber is greater than the old levelNumber and you are loading, it will split once
	else if (vars.levelNumGreater == 1 && vars.stillLoading == 0 && vars.isLoading.Current){
		vars.stillLoading = 1;
		return true;
	}
	//Activates when you stop loading
	else if (vars.stillLoading == 1 && vars.isLoading.Current){
		vars.stillLoading = 0;
		vars.levelNumGreater = 0;
	}
}

reset{
	//Allows restarting a level, but does not work in Throne Room
	if (current.levelNumber < old.levelNumber && old.levelNumber != 15){
		return true;
	}
}

exit{
	timer.IsGameTimePaused = true;
	vars.modLoadPointer = IntPtr.Zero;
	vars.modLoadWatcher = null;
	vars.isLoading = null;
}
