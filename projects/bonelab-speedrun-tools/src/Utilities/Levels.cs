using System.Collections.Generic;

namespace Sst.Utilities {
class Levels {
  public static Dictionary<string, byte> TitleToIndex =
      new Dictionary<string, byte>() {
        ["01 - Descent"] = 1,
        ["02 - BONELAB Hub"] = 2,
        ["03 - LongRun"] = 3,
        ["04 - Mine Dive"] = 4,
        ["05 - Big Anomaly"] = 5,
        ["06 - Street Puncher"] = 6,
        ["07 - Sprint Bridge 04"] = 7,
        ["08 - Magma Gate"] = 8,
        ["09- MoonBase"] = 9,
        ["10 - Monogon Motorway"] = 10,
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
}
}