using Sst.Utilities;
using SLZ.Marrow.Warehouse;
using SLZ.Bonelab;
using SLZ.SaveData;

namespace Sst.Common.Bonelab.HundredPercent {
public static class ProgressTracker {
  private static MenuProgressControl _menuProgressControl;

  public static GameProgress Calculate() {
    if (!_menuProgressControl) {
      _menuProgressControl = new MenuProgressControl();
      _menuProgressControl.descentCrate =
          new LevelCrateReference(Levels.Barcodes.DESCENT);
      _menuProgressControl.hubCrate =
          new LevelCrateReference(Levels.Barcodes.HUB);
      _menuProgressControl.longRunCrate =
          new LevelCrateReference(Levels.Barcodes.LONG_RUN);
      _menuProgressControl.mineDiveCrate =
          new LevelCrateReference(Levels.Barcodes.MINE_DIVE);
      _menuProgressControl.bigAnomalyACrate =
          new LevelCrateReference(Levels.Barcodes.BIG_ANOMALY_A);
      _menuProgressControl.streetPuncherCrate =
          new LevelCrateReference(Levels.Barcodes.STREET_PUNCHER);
      _menuProgressControl.sprintBridgeCrate =
          new LevelCrateReference(Levels.Barcodes.SPRINT_BRIDGE);
      _menuProgressControl.magmaGateCrate =
          new LevelCrateReference(Levels.Barcodes.MAGMA_GATE);
      _menuProgressControl.moonBaseCrate =
          new LevelCrateReference(Levels.Barcodes.MOON_BASE);
      _menuProgressControl.motorwayCrate =
          new LevelCrateReference(Levels.TITLE_MONOGON_MOTORWAY);
      _menuProgressControl.pillarCrate =
          new LevelCrateReference(Levels.Barcodes.PILLAR);
      _menuProgressControl.bigAnomalyBCrate =
          new LevelCrateReference(Levels.Barcodes.BIG_ANOMALY_B);
      _menuProgressControl.ascentCrate =
          new LevelCrateReference(Levels.Barcodes.ASCENT);
      _menuProgressControl.outroCrate =
          new LevelCrateReference(Levels.Barcodes.OUTRO);
    }

    var progression = DataManager.ActiveSave?.Progression;

    return new GameProgress() {
      Total = _menuProgressControl.SolveCompletePercent(),
      Arena = _menuProgressControl.CalcArenas(),
      Avatar = _menuProgressControl.CalcAvatars(),
      Campaign = _menuProgressControl.CalcCampaignComplete(),
      Experimental = _menuProgressControl.CalcExperimentals(),
      Parkour = _menuProgressControl.CalcParkours(),
      Sandbox = _menuProgressControl.CalcSandbox(),
      TacTrial = _menuProgressControl.CalcTacTrials(),
      EasterEggs = _menuProgressControl.CalcEasterEggs(),
      Unlocks = _menuProgressControl.CalcUnlocks(),
      Achievements = AchievementTracker.Progress,
      HasBeatGame = progression?.BeatGame ?? false,
      HasBodyLog = progression?.HasBodyLog ?? false,
    };
  }
}

public class GameProgress {
  public float Total;
  public float Arena;
  public float Avatar;
  public float Campaign;
  public float Experimental;
  public float Parkour;
  public float Sandbox;
  public float TacTrial;
  public float EasterEggs;
  public float Unlocks;

  public float Achievements;
  public bool HasBeatGame;
  public bool HasBodyLog;

  public bool IsComplete {
    get => HasBeatGame && HasBodyLog && Total >= 1f && Achievements >= 1f;
  }
}
}
