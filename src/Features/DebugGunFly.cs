using MelonLoader;
using UnityEngine;
using HarmonyLib;
using StressLevelZero.Rig;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpeedrunTools.Features {
class DebugGunFly : Feature {
  private int MAX_DATA_LENGTH = 10000;
  private static readonly string CSV_PATH =
      Path.Combine(Utils.DIR, "gun-fly-debug.csv");
  private static readonly string CSV_HEADER = string.Join(
      ",", new string[] { "Time", "IsFixedUpdate", "PlayerX", "PlayerY",
                          "PlayerZ", "GunX", "GunY", "GunZ" });

  public float UpdateFrequency = 1;
  public readonly Hotkey HotkeyToggle;

  private RigManager _rigManager;
  private StressLevelZero.Props.Weapons
      .HandWeaponSlotReciever[] _weaponReceivers = {};
  private List<float[]> _data;
  private float _lastFrameTime = 0f;
  private bool _isLastFrameFixedUpdate = false;
  private bool _isDebugging { get => _data != null; }

  private static GameObject[] FindInDescendants(GameObject root, string name) {
    var result = new List<GameObject>();
    _FindInDescendants(root.transform, name, ref result);
    return result.ToArray();
  }
  private static void _FindInDescendants(Transform transform, string name,
                                         ref List<GameObject> result) {
    if (transform.name == name)
      result.Add(transform.gameObject);
    for (int i = 0, count = transform.childCount; i < count; i++)
      _FindInDescendants(transform.GetChild(i), name, ref result);
  }

  public DebugGunFly() {
    HotkeyToggle = new Hotkey() { Predicate = (cl, cr) =>
                                      _rigManager != null && cr.GetThumbStick(),
                                  Handler = Toggle };
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    _rigManager = GameObject.FindObjectOfType<RigManager>();
    _weaponReceivers =
        FindInDescendants(_rigManager.gameWorldSkeletonRig.gameObject,
                          "WeaponReciever")
            .Select(wr => wr.GetComponent<StressLevelZero.Props.Weapons
                                              .HandWeaponSlotReciever>())
            .ToArray();
    MelonLogger.Msg($"found {_weaponReceivers.Count()} weapon recs");
  }

  public override void OnFixedUpdate() { AddDataFrame(true); }

  public override void OnUpdate() { AddDataFrame(false); }

  private void AddDataFrame(bool isFixedUpdate) {
    if (!_isDebugging)
      return;

    if (_lastFrameTime != 0f) {
      var playerPos = _rigManager.physicsRig.transform.position;
      var receiverWithWeapon =
          _weaponReceivers.FirstOrDefault(wr => wr.m_SlottedWeapon != null);
      if (receiverWithWeapon == null) {
        MelonLogger.Warning("No weapon found, stopping recording");
        Toggle();
        return;
      }
      var gunPos = receiverWithWeapon.m_WeaponHost.transform.position;
      _data.Add(new float[] { _lastFrameTime, _isLastFrameFixedUpdate ? 1f : 0f,
                              playerPos.x, playerPos.y, playerPos.z, gunPos.x,
                              gunPos.y, gunPos.z });
    }

    _lastFrameTime = Time.time;
    _isLastFrameFixedUpdate = isFixedUpdate;

    if (_data.Count >= MAX_DATA_LENGTH) {
      MelonLogger.Warning("Gun fly debug data limit reached");
      Toggle();
    }
  }

  private void Toggle() {
    if (_isDebugging) {
      MelonLogger.Msg("Gun fly debug stop");
      File.WriteAllLines(CSV_PATH,
                         new string[] { CSV_HEADER }.Concat(_data.Select(
                             dataPoints => string.Join(",", dataPoints))));
      _data = null;
      _lastFrameTime = 0f;
    } else {
      MelonLogger.Msg("Gun fly debug start");
      _data = new List<float[]>();
    }
  }
}
}
