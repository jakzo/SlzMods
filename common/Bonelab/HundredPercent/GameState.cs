namespace Sst.Common.Bonelab.HundredPercent {
public class GameState {
  public const string NAMED_PIPE = "BonelabHundredPercent";

  public bool isComplete;
  public bool beatGame;
  public bool isLoading;
  public string levelBarcode;
  public int capsulesUnlocked;
  public int capsulesTotal;
  public string capsuleJustUnlocked;
  public int achievementsUnlocked;
  public int achievementsTotal;
  public string achievementJustUnlocked;
  public float percentageComplete;
  public float percentageTotal = 1f;
  public string arenaJustCompleted;
}
}
