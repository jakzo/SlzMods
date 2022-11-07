// Reverse engineered using Cpp2IlDumper, dnSpy and Ghidra.
// I took some liberties to modify the logic and APIs for readability but the
// methods on this class should still output the same result as the originals.
namespace CompletionPseudoCode {
class MenuProgressControl {
  // Returns [0, 0.95] (would be [0, 1] if CalcEasterEggs worked)
  public float SolveCompletePercent() =>
      CalcCampaignComplete() * 0.59f + CalcTacTrials() * 0.03f +
      CalcArenas() * 0.04f + CalcParkours() * 0.04f + CalcSandbox() * 0.05f +
      CalcExperimentals() * 0.04f + CalcUnlocks() * 0.10f +
      CalcAvatars() * 0.06f + CalcEasterEggs() * 0.05f;

  // Returns [0, 1]
  public float CalcCampaignComplete() {
    var progression = DataManager.ActiveSave.Progression;
    var currentLevel = progression.CurrentCampaignLevel == "Hub"
                           ? progression.BodyLogEnabled ? "Ascent" : "LongRun"
                           : progression.CurrentCampaignLevel;

    // Total of level progress + hub progress is 100
    var levels = new(string, float, float)[] {
      ("Descent", 9, 7),     ("LongRun", 10, 5),      ("BigAnomalyA", 5, 0),
      ("BigAnomalyA", 9, 0), ("StreetPuncher", 2, 0), ("SprintBridge", 2, 0),
      ("MagmaGate", 2, 0),   ("MoonBase", 2, 0),      ("MonogonMotorway", 2, 0),
      ("Pillar", 2, 0),      ("BigAnomalyB", 4, 0),   ("Ascent", 19, 7),
      ("Outro", 10, 0),
    };
    var result = CalcHubComplete();
    foreach (var (name, maxLevelProgress, maxLevelCheckpoints) in levels) {
      if (!progression.BeatGame && currentLevel == name) {
        var numCheckpoints = BonelabProgressionHelper.TryGetLevelProgress(
            progression, currentLevel);
        if (maxLevelCheckpoints > 0)
          result +=
              ValueRemapper(numCheckpoints, 0, maxLevelCheckpoints, 0, 1) *
              maxLevelProgress;
        return result * 0.01f;
      }
      result += maxLevelProgress;
    }
    return progression.BeatGame ? result : 0;
  }

  // Returns [0, 22]
  public float CalcHubComplete() {
    var progress = 0.0f;
    foreach (var flag in new[] {
               "SLZ.Bonelab.TacTrialKeyUnlocked",
               "SLZ.Bonelab.ArenaKeyUnlocked",
               "SLZ.Bonelab.SandboxKeyUnlocked",
               "SLZ.Bonelab.ParkourKeyUnlocked",
               "SLZ.Bonelab.ExperimentalKeyUnlocked",
               "SLZ.Bonelab.ModKeyUnlocked",
               "SLZ.Bonelab.JimmyKeyUnlocked",
               "SLZ.Bonelab.JimmyKeyPlaced",
             }) {
      if (HubUtilities.GetHubFlag(DataManager.ActiveSave.Progression, flag))
        progress += 5;
    }
    foreach (var flag in new[] {
               "SLZ.Bonelab.TacTrialKeyPlaced",
               "SLZ.Bonelab.ArenaKeyPlaced",
               "SLZ.Bonelab.SandboxKeyPlaced",
               "SLZ.Bonelab.ParkourKeyPlaced",
               "SLZ.Bonelab.ExperimentalKeyPlaced",
               "SLZ.Bonelab.ModKeyPlaced",
             }) {
      if (HubUtilities.GetHubFlag(DataManager.ActiveSave.Progression, flag))
        progress += 10;
    }
    // At this point progress is max 100
    return progress * 0.01f * 22;
  }

  // Returns [0, 1]
  public float CalcTacTrials() {
    var totalCompleted = 0;
    var progression = DataManager.ActiveSave.Progression;
    if (BonelabProgressionHelper.TryGetLevelCompleted(progression, "District"))
      totalCompleted++;
    if (BonelabProgressionHelper.TryGetLevelCompleted(progression,
                                                      "ThreeGunRange"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "StreetPuncher"))
      totalCompleted++;
    return totalCompleted / 3.0f;
  }

  // Returns [0, 1]
  public float CalcArenas() {
    var totalCompleted = 0;
    var progression = DataManager.ActiveSave.Progression;
    if (BonelabProgressionHelper.TryGetLevelCompleted(progression,
                                                      "FantasyArena"))
      totalCompleted++;
    if (BonelabProgressionHelper.TryGetLevelCompleted(progression,
                                                      "ZWarehouse"))
      totalCompleted++;
    if (BonelabProgressionHelper.TryGetLevelCompleted(progression,
                                                      "TunnelTipper"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "MagmaGate"))
      totalCompleted++;
    return totalCompleted * 0.25f;
  }

  // Returns [0, 1]
  public float CalcParkours() {
    var totalCompleted = 0;
    var progression = DataManager.ActiveSave.Progression;
    if (BonelabProgressionHelper.TryGetLevelCompleted(progression, "Rooftops"))
      totalCompleted++;
    if (BonelabProgressionHelper.TryGetLevelCompleted(progression,
                                                      "DistrictParkour"))
      totalCompleted++;
    if (BonelabProgressionHelper.TryGetLevelCompleted(progression,
                                                      "DungeonWarrior"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "SprintBridge"))
      totalCompleted++;
    return totalCompleted * 0.25f;
  }

  // Returns [0, 1]
  public float CalcSandbox() {
    var totalCompleted = 0;
    var progression = DataManager.ActiveSave.Progression;
    if (BonelabGameControl.IsCompleted(progression, "Tuscany"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "MuseumSandbox"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "Holodeck"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "HalfwayPark"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "MoonBase"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "GunRangeSandbox"))
      totalCompleted++;
    return totalCompleted / 6.0f;
  }

  // Returns [0, 1]
  public float CalcExperimentals() {
    var totalCompleted = 0;
    var progression = DataManager.ActiveSave.Progression;
    if (BonelabGameControl.IsCompleted(progression, "Baseline"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "Mirror"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "KartBowling"))
      totalCompleted++;
    if (BonelabGameControl.IsCompleted(progression, "MonogonMotorway"))
      totalCompleted++;
    return totalCompleted * 0.25f;
  }

  // Returns [0, 1]
  public float CalcAvatars() {
    var totalCompleted = 0;
    foreach (var levelName in new[] {
               "StreetPuncher",
               "SprintBridge",
               "MagmaGate",
               "MoonBase",
               "MonogonMotorway",
               "Pillar",
             }) {
      if (BonelabGameControl.IsCompleted(DataManager.ActiveSave.Progression,
                                         levelName))
        totalCompleted++;
    }
    return ValueRemapper(totalCompleted, 0, 6, 0, 1);
  }

  // Returns [0, 1]
  public float CalcUnlocks() {
    // 174
    var totalNumUnlocks =
        AssetWarehouseExtensions
            .Filter(AssetWarehouse.Instance.GetCrates(),
                    new CrateFilters.UnlockableAndNotRedactedCrateFilter())
            .Count;
    var currentNumUnlocked =
        AssetWarehouseExtensions
            .Filter(AssetWarehouse.Instance.GetCrates(),
                    new CrateFilters.UnlockedAndNotRedactedCrateFilter())
            .Count;
    return ValueRemapper(currentNumUnlocked, 0, totalNumUnlocks, 0, 1);
  }

  // Returns [0, 0] uhhh...
  public float CalcEasterEggs() => 0;

  private static float ValueRemapper(float fromValue, float fromLow,
                                     float fromHigh, float toLow,
                                     float toHigh) {
    if (fromLow == fromHigh)
      return toLow;
    var factor = Clamp((fromValue - fromLow) / (fromHigh - fromLow), 0, 1);
    return (toHigh - toLow) * factor + toLow;
  }
}
}
