using System.Linq;
using MelonLoader;
using UnityEngine;
using SLZ.Props;
using SLZ.Marrow.Data;
using SLZ.Rig;
using SLZ.Bonelab;

namespace Sst.LootDropBugfix {
public static class AmmoDebugger {
  private static MelonPreferences_Entry<bool> _prefDebugAmmo;
  private static float _lastUpdate = 0;
  private static ObjectDestructable _toBreak;
  private static bool _replacedAmmo = false;

  public static void Initialize() {
    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefDebugAmmo = category.CreateEntry(
        "debug_ammo", false,
        "Teleports and breaks ammo boxes to test item replacement"
    );
  }

  public static void OnAmmoReplaced() { _replacedAmmo = true; }

  public static void OnUpdate() {
    if (!_prefDebugAmmo.Value || Time.time - _lastUpdate <= 0.5f ||
        _replacedAmmo)
      return;

    _lastUpdate = Time.time;
    if (_toBreak) {
      _toBreak.TakeDamage(Vector3.back, 100, false, AttackType.Piercing);
      _toBreak = null;
    } else {
      foreach (var ammo in GameObject.FindObjectsOfType<AmmoPickup>())
        GameObject.Destroy(ammo.transform.parent.gameObject);
      var head = GameObject.FindObjectOfType<RigManager>().physicsRig.m_head;
      var ammoCrates =
          GameObject.FindObjectsOfType<ObjectDestructable>()
              .Where(
                  obj => obj.lootTable != null && Mod.IsAmmoCrate(obj) &&
                      (obj.transform.position - head.position).sqrMagnitude > 25
              )
              .ToArray();
      if (ammoCrates.Length > 0) {
        _toBreak = ammoCrates[0];
        _toBreak.transform.position =
            head.position + head.rotation * new Vector3(0, 0, 2);
      }
    }
  }
}
}
