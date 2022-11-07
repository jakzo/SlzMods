using System.Collections.Generic;
using System.Linq;
using SLZ.Marrow.Warehouse;

namespace Sst.Utilities {
class Levels {
  public const string TITLE_DESCENT = "01 - Descent";
  public const string TITLE_MONOGON_MOTORWAY = "10 - Monogon Motorway";

  public static Dictionary<string, byte> TitleToIndex =
      new Dictionary<string, byte>() {
        [TITLE_DESCENT] = 1,
        ["02 - BONELAB Hub"] = 2,
        ["03 - LongRun"] = 3,
        ["04 - Mine Dive"] = 4,
        ["05 - Big Anomaly"] = 5,
        ["06 - Street Puncher"] = 6,
        ["07 - Sprint Bridge 04"] = 7,
        ["08 - Magma Gate"] = 8,
        ["09- MoonBase"] = 9,
        [TITLE_MONOGON_MOTORWAY] = 10,
        ["11 - Pillar Climb"] = 11,
        ["12 - Big Anomaly B"] = 12,
        ["13 - Ascent"] = 13,
        ["14 - Home"] = 14,
        ["15 - Void G114"] = 15,
        ["Big Bone Bowling"] = 16,
        ["Container Yard"] = 17,
        ["Neon District Parkour"] = 18,
        ["Neon District Tac Trial"] = 19,
        ["Drop Pit"] = 20,
        ["Dungeon Warrior"] = 21,
        ["Fantasy Arena"] = 22,
        ["Gun Range"] = 23,
        ["Halfway Park"] = 24,
        ["HoloChamber"] = 25,
        ["Mirror"] = 26,
        ["Museum Basement"] = 27,
        ["Rooftops"] = 28,
        ["Tunnel Tipper"] = 29,
        ["Tuscany"] = 30,
        ["00 - Main Menu"] = 31,
        ["Baseline"] = 32,
        ["Load Default"] = 33,
        ["Load Mod"] = 34,
      };

  public static byte GetIndex(string title) {
    byte index;
    if (!TitleToIndex.TryGetValue(title, out index))
      return 0;
    return index;
  }

  public static LevelCrateReference CRATE_DESCENT =
      new LevelCrateReference("c2534c5a-4197-4879-8cd3-4a695363656e");
  public static LevelCrateReference CRATE_HUB =
      new LevelCrateReference("c2534c5a-6b79-40ec-8e98-e58c5363656e");
  public static LevelCrateReference CRATE_LONG_RUN =
      new LevelCrateReference("c2534c5a-56a6-40ab-a8ce-23074c657665");
  public static LevelCrateReference CRATE_MINE_DIVE =
      new LevelCrateReference("c2534c5a-54df-470b-baaf-741f4c657665");
  public static LevelCrateReference CRATE_BIG_ANOMALY_A =
      new LevelCrateReference("c2534c5a-7601-4443-bdfe-7f235363656e");
  public static LevelCrateReference CRATE_STREET_PUNCHER =
      new LevelCrateReference("SLZ.BONELAB.Content.Level.LevelStreetPunch");
  public static LevelCrateReference CRATE_SPRINT_BRIDGE =
      new LevelCrateReference("SLZ.BONELAB.Content.Level.SprintBridge04");
  public static LevelCrateReference CRATE_MAGMA_GATE =
      new LevelCrateReference("SLZ.BONELAB.Content.Level.SceneMagmaGate");
  public static LevelCrateReference CRATE_MOON_BASE =
      new LevelCrateReference("SLZ.BONELAB.Content.Level.MoonBase");
  public static LevelCrateReference CRATE_MONOGON_MOTORWAY =
      new LevelCrateReference("SLZ.BONELAB.Content.Level.LevelKartRace");
  public static LevelCrateReference CRATE_PILLAR =
      new LevelCrateReference("c2534c5a-c056-4883-ac79-e051426f6964");
  public static LevelCrateReference CRATE_BIG_ANOMALY_B =
      new LevelCrateReference("SLZ.BONELAB.Content.Level.LevelBigAnomalyB");
  public static LevelCrateReference CRATE_ASCENT =
      new LevelCrateReference("c2534c5a-db71-49cf-b694-24584c657665");
  public static LevelCrateReference CRATE_OUTRO =
      new LevelCrateReference("SLZ.BONELAB.Content.Level.LevelOutro");

  public static LevelCrateReference[] CAMPAIGN_LEVELS = {
    CRATE_DESCENT,          CRATE_HUB,           CRATE_LONG_RUN,
    CRATE_MINE_DIVE,        CRATE_BIG_ANOMALY_A, CRATE_STREET_PUNCHER,
    CRATE_SPRINT_BRIDGE,    CRATE_MAGMA_GATE,    CRATE_MOON_BASE,
    CRATE_MONOGON_MOTORWAY, CRATE_PILLAR,        CRATE_BIG_ANOMALY_B,
    CRATE_ASCENT,           CRATE_OUTRO,
  };

  public static HashSet<string> CAMPAIGN_LEVEL_BARCODES =
      CAMPAIGN_LEVELS.Select(level => level.Barcode.ID).ToHashSet();
}
}
