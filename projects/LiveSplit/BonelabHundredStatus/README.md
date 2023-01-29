Shows game progress and achievements for Bonelab 100% speedruns in LiveSplit. Also acts as an autosplitter so the timer mod is not necessary.

## Installation

- Download [BonelabHundredStatus.dll](projects/LiveSplit/BonelabHundredStatus/Components/BonelabHundredStatus.dll) (right-click on the link -> save as...)
- Copy `BonelabHundredStatus.dll` into `LIVESPLIT/Components/BonelabHundredStatus.dll` (the `LIVESPLIT` directory depends on where you've installed LiveSplit to)
- Download [Newtonsoft.Json from nuget](https://www.nuget.org/packages/Newtonsoft.Json/) ("download package" link on the side)
- Open as ZIP (eg. rename from `.nupkg` to `.zip`)
- Copy `lib\net45\Newtonsoft.Json.dll` into `LIVESPLIT/Components/Newtonsoft.Json.dll`
- Open LiveSplit and edit layout
- Add (+) -> Other -> Bonelab 100% Status/Autosplitter
