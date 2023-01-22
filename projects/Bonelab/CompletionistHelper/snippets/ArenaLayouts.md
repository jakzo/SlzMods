## Arena Geos

- Dynamic
- Arches
- Maze
- Fence ring
- Death pit
- Castle
- Box forest

## Debugging Geo Switching

To find how to switch geos, use unity explorer to add hooks to:

- `GeoManager.IncrementAndToggleGeo()`
- `GeoManager.IncrementGeoIndex()`
- `ArenaMenuController.ToggleGeo()` (calls `GeoManager.IncrementGeoIndex()`)
- `Arena_GameController.ARENA_StartMatch()` (calls `GeoManager.IncrementAndToggleGeo()`)
- `Arena_GameController.OnArenaSessionBegin()` (calls `GeoManager.IncrementAndToggleGeo()`)
- `Arena_GameController.EndOfRound()` (calls `GeoManager.IncrementAndToggleGeo()`)
- `Arena_GameController.ProgressToNextProfile()` (calls `GeoManager.IncrementAndToggleGeo()`)
- `ArenaMenuController.SurvivalSelect()` (calls `Arena_GameController.ARENA_StartMatch()` which calls `GeoManager.IncrementAndToggleGeo()`)
