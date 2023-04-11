using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Rig;
using SLZ.Props.Weapons;
using System.Linq;

namespace Sst.GunGlitcher {
public class Mod : MelonMod {
  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  public override void OnUpdate() {
    if (IsControlDown() && Input.GetKeyDown(KeyCode.G)) {
      var rigManager = GameObject.FindObjectOfType<RigManager>();
      var heldGun =
          new[] { rigManager.physicsRig.leftHand,
                  rigManager.physicsRig.rightHand }
              .Select(hand => hand.AttachedReceiver?.Host?.GetHostGameObject()
                                  ?.GetComponent<Gun>())
              .Where(gun => gun && gun.HasMagazine())
              .FirstOrDefault();
      if (heldGun)
        TriggerGunGlitch(heldGun);
    }
  }

  private void TriggerGunGlitch(Gun gun) {
    var socket = gun.GetComponentInChildren<SLZ.Interaction.AmmoSocket>();
    socket.EjectMagazine();
    socket._magazinePlug._lastSocket = null;
    socket.proxyGrip.ForceDetach();
    socket.Unlock();
  }

  private bool IsControlDown() =>
      Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

  [HarmonyPatch(typeof(Control_GlobalTime), nameof(Control_GlobalTime.Start))]
  class Control_GlobalTime_Start_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(Control_GlobalTime __instance) {
      __instance.max_timeScaleStep = int.MaxValue;
      __instance.max_intensity = float.PositiveInfinity;
    }
  }
}
}
