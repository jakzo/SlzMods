using MelonLoader;
using UnityEngine;
using HarmonyLib;

namespace SpeedrunTools.Features {
class Tas : Feature {

  // [HarmonyPatch(typeof(Time), nameof(Time.time))]
  // class Time_time_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix() {}
  // }

  // [HarmonyPatch(typeof(Rigidbody), nameof(Time.time))]
  // class Time_time_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix() {}
  // }
}
}
