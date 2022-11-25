using Sst.Utilities;
using SLZ.Marrow.Warehouse;
using SLZ.Bonelab;

namespace Sst.CompletionistHelper {
class Progress {
  public const float MAX_PROGRESS = 0.95f;

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

  public bool IsComplete { get => Total >= MAX_PROGRESS; }

  private MenuProgressControl _menuProgressControl;

  public void Refresh() {
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

    Total = _menuProgressControl.SolveCompletePercent();
    Arena = _menuProgressControl.CalcArenas();
    Avatar = _menuProgressControl.CalcAvatars();
    Campaign = _menuProgressControl.CalcCampaignComplete();
    Experimental = _menuProgressControl.CalcExperimentals();
    Parkour = _menuProgressControl.CalcParkours();
    Sandbox = _menuProgressControl.CalcSandbox();
    TacTrial = _menuProgressControl.CalcTacTrials();
    EasterEggs = _menuProgressControl.CalcEasterEggs();
    Unlocks = _menuProgressControl.CalcUnlocks();
  }
}
}
