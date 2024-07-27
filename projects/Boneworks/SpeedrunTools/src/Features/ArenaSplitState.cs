using MelonLoader;
using HarmonyLib;
using UnityEngine;
using StressLevelZero.Arena;

namespace Sst.Features {
class ArenaSplitState : Feature {
  // State that the ASL finds using signature scanning
  private static byte[] State = {
    // 0 = magic string start
    // Signature is set dynamically to avoid finding this hardcoded array
    0x00, // 0xD5
    0xE2,
    0x03,
    0x34,
    0xC2,
    0xDF,
    0x63,
    // 7 = arena index
    0x00,
  };

  private static bool _isCurrentChallengeCompleted = true;

  public ArenaSplitState() { State[0] = 0xD5; }

  [HarmonyPatch(
      typeof(Arena_GameManager), nameof(Arena_GameManager.RingTheBell)
  )]
  class ArenaGameManager_RingTheBell_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(Arena_GameManager __instance) {
      _isCurrentChallengeCompleted =
          __instance.activeChallenge?.profile?.completedHard ?? true;
    }
  }

  [HarmonyPatch(
      typeof(Arena_GameManager),
      nameof(Arena_GameManager.SaveChallengeCompletion)
  )]
  class ArenaGameManager_SaveChallengeCompletion_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(Arena_GameManager __instance) {
      if (!_isCurrentChallengeCompleted &&
          __instance.activeChallenge.profile.completedHard) {
        _isCurrentChallengeCompleted = true;
        State[7]++;
      }
    }
  }
}
}
