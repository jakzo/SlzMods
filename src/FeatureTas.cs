using MelonLoader;
using UnityEngine;
using HarmonyLib;

namespace SpeedrunTools {
class FeatureTas : Feature {

  // [HarmonyPatch(typeof(Time), nameof(Time.time))]
  // class Time_time_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix() {}
  // }
}
}
