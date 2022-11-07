using UnityEngine;
using SLZ.Marrow.Warehouse;

namespace Sst {
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
  public float Unlocks;

  public bool IsComplete { get => Total >= MAX_PROGRESS; }

  private MenuProgressControl _menuProgressControl;

  public void Refresh() {
    if (!_menuProgressControl) {
      _menuProgressControl = new MenuProgressControl();
      _menuProgressControl.descentCrate =
          new LevelCrateReference("c2534c5a-4197-4879-8cd3-4a695363656e");
      _menuProgressControl.hubCrate =
          new LevelCrateReference("c2534c5a-6b79-40ec-8e98-e58c5363656e");
      _menuProgressControl.longRunCrate =
          new LevelCrateReference("c2534c5a-56a6-40ab-a8ce-23074c657665");
      _menuProgressControl.mineDiveCrate =
          new LevelCrateReference("c2534c5a-54df-470b-baaf-741f4c657665");
      _menuProgressControl.bigAnomalyACrate =
          new LevelCrateReference("c2534c5a-7601-4443-bdfe-7f235363656e");
      _menuProgressControl.streetPuncherCrate =
          new LevelCrateReference("SLZ.BONELAB.Content.Level.LevelStreetPunch");
      _menuProgressControl.sprintBridgeCrate =
          new LevelCrateReference("SLZ.BONELAB.Content.Level.SprintBridge04");
      _menuProgressControl.magmaGateCrate =
          new LevelCrateReference("SLZ.BONELAB.Content.Level.SceneMagmaGate");
      _menuProgressControl.moonBaseCrate =
          new LevelCrateReference("SLZ.BONELAB.Content.Level.MoonBase");
      _menuProgressControl.motorwayCrate =
          new LevelCrateReference("SLZ.BONELAB.Content.Level.LevelKartRace");
      _menuProgressControl.pillarCrate =
          new LevelCrateReference("c2534c5a-c056-4883-ac79-e051426f6964");
      _menuProgressControl.bigAnomalyBCrate =
          new LevelCrateReference("SLZ.BONELAB.Content.Level.LevelBigAnomalyB");
      _menuProgressControl.ascentCrate =
          new LevelCrateReference("c2534c5a-db71-49cf-b694-24584c657665");
      _menuProgressControl.outroCrate =
          new LevelCrateReference("SLZ.BONELAB.Content.Level.LevelOutro");
    }

    Total = _menuProgressControl.SolveCompletePercent();
    Arena = _menuProgressControl.CalcArenas();
    Avatar = _menuProgressControl.CalcAvatars();
    Campaign = _menuProgressControl.CalcCampaignComplete();
    Experimental = _menuProgressControl.CalcExperimentals();
    Parkour = _menuProgressControl.CalcParkours();
    Sandbox = _menuProgressControl.CalcSandbox();
    TacTrial = _menuProgressControl.CalcTacTrials();
    Unlocks = _menuProgressControl.CalcUnlocks();
  }
}
}
