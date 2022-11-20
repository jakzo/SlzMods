namespace Sst.Common.Bonelab {
class HundredPercent {
  public const string NAMED_PIPE = "BonelabHundredPercent";

  public class GameState {
    public bool isLoading;
    public string levelName;
    public int capsulesUnlocked;
    public int capsulesTotal;
    public string[] capsulesJustUnlocked;
    public int achievementsUnlocked;
    public int achievementsTotal;
    public string[] achievementsJustUnlocked;
    public float percentageComplete;
    public float percentageTotal;
  }
}
}
