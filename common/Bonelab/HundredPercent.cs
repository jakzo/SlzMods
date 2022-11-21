namespace Sst.Common.Bonelab {
public class HundredPercent {
  public const string NAMED_PIPE = "BonelabHundredPercent";

  public class GameState {
    public bool isComplete = false;
    public bool isLoading = false;
    public string levelBarcode = null;
    public int capsulesUnlocked = 0;
    public int capsulesTotal = 0;
    public string[] capsulesJustUnlocked = null;
    public int achievementsUnlocked = 0;
    public int achievementsTotal = 0;
    public string[] achievementsJustUnlocked = null;
    public float percentageComplete = 0;
    public float percentageTotal = 0;
  }
}
}
