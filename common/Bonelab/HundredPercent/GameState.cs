namespace Sst.Common.Bonelab.HundredPercent {
public class GameState {
  public const string NAMED_PIPE = "BonelabHundredPercent";

  public bool isComplete;
  public bool isLoading;
  public string levelBarcode;
  public int capsulesUnlocked;
  public int capsulesTotal;
  public string[] capsulesJustUnlocked;
  public int achievementsUnlocked;
  public int achievementsTotal;
  public string[] achievementsJustUnlocked;
  public float percentageComplete;
  public float percentageTotal = 1f;
}
}
