using System.Collections.Generic;
using System.Linq;

namespace Sst.Common.Boneworks {
public class HundredPercentState {
  public const string NAMED_PIPE = "BoneworksHundredPercent";
  public const string TYPE_AMMO_LIGHT = "ammo_light";
  public const string TYPE_AMMO_MEDIUM = "ammo_medium";
  public const string TYPE_ITEM = "item";

  public Dictionary<string, RngState> rngUnlocks =
      new[] {
        ("Baseball", "81207815-e447-430b-9ea6-6d8c35842fef", 0.1f),
        ("Golf Club", "290780a8-4f88-451a-88a5-1599f8e7e89f", 0.02f),
        ("Baton", "53e4ff47-b0f4-426c-956d-ed391ca6f5f7", 0.1f),
      }
          .ToDictionary(def => def.Item2, def => new RngState() {
            name = def.Item1,
            attempts = 0,
            prevAttemptChance = def.Item3,
            probabilityNotDroppedYet = 1f,
            hasDropped = false,
          });
  public int unlockLevelCount;
  public int unlockLevelMax;
  public int ammoLevelCount;
  public int ammoLevelMax;
  public Collectible[] justCollected;
  public Collectible[] levelCollectibles;

  public class Collectible {
    public string Type;
    public string Uuid;
    public string DisplayName;
  }

  public class RngState {
    public string name;
    public int attempts;
    public float prevAttemptChance;
    public float probabilityNotDroppedYet;
    public bool hasDropped;
  }
}
}
