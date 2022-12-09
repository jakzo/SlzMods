using System.Collections.Generic;
using System.Linq;

namespace Sst.Utilities {
public class Levels {
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

  public class Barcodes {
    public const string DESCENT = "c2534c5a-4197-4879-8cd3-4a695363656e";
    public const string HUB = "c2534c5a-6b79-40ec-8e98-e58c5363656e";
    public const string LONG_RUN = "c2534c5a-56a6-40ab-a8ce-23074c657665";
    public const string MINE_DIVE = "c2534c5a-54df-470b-baaf-741f4c657665";
    public const string BIG_ANOMALY_A = "c2534c5a-7601-4443-bdfe-7f235363656e";
    public const string STREET_PUNCHER =
        "SLZ.BONELAB.Content.Level.LevelStreetPunch";
    public const string SPRINT_BRIDGE =
        "SLZ.BONELAB.Content.Level.SprintBridge04";
    public const string MAGMA_GATE = "SLZ.BONELAB.Content.Level.SceneMagmaGate";
    public const string MOON_BASE = "SLZ.BONELAB.Content.Level.MoonBase";
    public const string MONOGON_MOTORWAY =
        "SLZ.BONELAB.Content.Level.LevelKartRace";
    public const string PILLAR = "c2534c5a-c056-4883-ac79-e051426f6964";
    public const string BIG_ANOMALY_B =
        "SLZ.BONELAB.Content.Level.LevelBigAnomalyB";
    public const string ASCENT = "c2534c5a-db71-49cf-b694-24584c657665";
    public const string OUTRO = "SLZ.BONELAB.Content.Level.LevelOutro";
    public const string ROOFTOPS = "c2534c5a-c6ac-48b4-9c5f-b5cd5363656e";
    public const string TUNNEL_TIPPER = "c2534c5a-c180-40e0-b2b7-325c5363656e";
    public const string DISTRICT_TAC_TRIAL =
        "c2534c5a-4f3b-480e-ad2f-69175363656e";
    public const string DROP_PIT = "c2534c5a-de61-4df9-8f6c-416954726547";
    public const string TUSCANY = "c2534c5a-2c4c-4b44-b076-203b5363656e";
  }

  public static string[] CAMPAIGN_LEVEL_BARCODES = {
    Barcodes.DESCENT,          Barcodes.HUB,           Barcodes.LONG_RUN,
    Barcodes.MINE_DIVE,        Barcodes.BIG_ANOMALY_A, Barcodes.STREET_PUNCHER,
    Barcodes.SPRINT_BRIDGE,    Barcodes.MAGMA_GATE,    Barcodes.MOON_BASE,
    Barcodes.MONOGON_MOTORWAY, Barcodes.PILLAR,        Barcodes.BIG_ANOMALY_B,
    Barcodes.ASCENT,           Barcodes.OUTRO,
  };

  public static HashSet<string> CAMPAIGN_LEVEL_BARCODES_SET =
      CAMPAIGN_LEVEL_BARCODES.ToHashSet();
}
}
