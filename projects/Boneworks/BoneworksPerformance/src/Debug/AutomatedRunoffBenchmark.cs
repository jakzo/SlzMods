#if DEBUG
using System;
using MelonLoader;
using StressLevelZero.Props.Weapons;
using StressLevelZero.Pool;
using StressLevelZero.Rig;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sst.BoneworksPerformance;

internal sealed class AutomatedRunoffBenchmark {
  public const int RunoffBuildIndex = 6;
  private const int SetupDelayFrames = 120;

  // Captured through MelonMCP during a real Runoff gun fly on 2026-08-30.
  private static readonly Vector3 PlayerPosition =
      new Vector3(112.849f, 30.39373f, -88.5749f);
  private static readonly Vector3 MagazineLocalPosition =
      new Vector3(-0.080001995f, 0.0060969002f, 0.0000008174384f);
  private static readonly Quaternion MagazineLocalRotation =
      new Quaternion(
          -0.7071071f, -0.0000003228868f,
          -0.70710655f, 0.00000004081285f
      );
  private static readonly Vector3 GunLocalPosition =
      new Vector3(0f, -0.22300055f, -0.09200477f);
  private static readonly Quaternion GunLocalRotation =
      new Quaternion(0.5396567f, 0.4140921f, -0.5815333f, 0.4462258f);

  private readonly MelonPreferences_Entry<bool> _enabled;
  private readonly MelonPreferences_Entry<bool> _autoLoadRunoff;
  private readonly bool _forceEnabled;
  private RigManager _rig;
  private HandWeaponSlotReciever _weaponSlotReceiver;
  private Gun _gun;
  private Magazine _magazine;
  private Rigidbody _footballBody;
  private RigidbodyConstraints _originalFootballConstraints;
  private int _sceneFrames;
  private bool _loadRequested;
  private bool _warned;
  private bool _teleported;

  public bool IsReady { get; private set; }
  public bool IsEnabled => _forceEnabled || _enabled.Value;
  internal RigManager Rig => _rig;
  internal HandWeaponSlotReciever WeaponSlotReceiver => _weaponSlotReceiver;
  internal Gun Gun => _gun;
  internal Magazine Magazine => _magazine;

  public AutomatedRunoffBenchmark(
      MelonPreferences_Entry<bool> enabled,
      MelonPreferences_Entry<bool> autoLoadRunoff,
      bool forceEnabled
  ) {
    _enabled = enabled;
    _autoLoadRunoff = autoLoadRunoff;
    _forceEnabled = forceEnabled;
  }

  public void OnSceneWasInitialized(int buildIndex) {
    if (buildIndex != RunoffBuildIndex)
      return;
    UnlockFootball();
    _rig = null;
    _weaponSlotReceiver = null;
    _gun = null;
    _magazine = null;
    _sceneFrames = 0;
    _warned = false;
    _teleported = false;
    IsReady = false;
    _loadRequested = false;
  }

  public void OnUpdate() {
    if (!IsEnabled)
      return;

    var scene = SceneManager.GetActiveScene();
    if (scene.buildIndex != RunoffBuildIndex) {
      if (_autoLoadRunoff.Value && !_loadRequested && scene.buildIndex == 1 &&
          ++_sceneFrames >= SetupDelayFrames) {
        _loadRequested = true;
        MelonLogger.Msg("Automated benchmark loading Runoff.");
        SceneManager.LoadScene(RunoffBuildIndex);
      }
      return;
    }

    if (!IsReady) {
      if (++_sceneFrames < SetupDelayFrames)
        return;
      if (!TrySetUp())
        return;
    }
  }

  public void Shutdown() => UnlockFootball();

  internal bool EnsureMagazineChestContact() {
    if (!_gun || !_magazine)
      return false;
    var gunTransform = _gun.host ? _gun.host.transform : _gun.transform;
    return SeatMagazineAgainstChest(gunTransform);
  }

  private bool TrySetUp() {
    _rig = UnityEngine.Object.FindObjectOfType<RigManager>();
    if (!_rig)
      return false;
    if (!_teleported) {
      TeleportPlayer();
      _teleported = true;
    }

    var receivers = UnityEngine.Object.FindObjectsOfType<HandWeaponSlotReciever>();
    for (var i = 0; i < receivers.Length; i++) {
      if (Convert.ToInt32(receivers[i].saveBodySlotType) == 2)
        _weaponSlotReceiver = receivers[i];
      var host = receivers[i].m_WeaponHost;
      if (!host)
        continue;
      var candidate = host.GetComponent<Gun>();
      if (candidate && candidate.name.IndexOf(
              "M16", StringComparison.OrdinalIgnoreCase
          ) >= 0) {
        _gun = candidate;
      }
    }
    if (!_gun)
      _gun = FindAnyM16();
    if (!_gun)
      _gun = SpawnM16();
    if (!_gun) {
      WarnOnce("Waiting for an M16 spawnable pool.");
      return false;
    }

    if (_weaponSlotReceiver) {
      if (_weaponSlotReceiver.m_WeaponHost != _gun.host)
        _weaponSlotReceiver.OnInteractableHostEnter(_gun.host);
      var slotParent = _weaponSlotReceiver.enableUpdateVolume
          ? _weaponSlotReceiver.transform.parent
          : _weaponSlotReceiver.transform;
      var gunTransform = _gun.host ? _gun.host.transform : _gun.transform;
      gunTransform.SetParent(slotParent, false);
      gunTransform.localPosition = GunLocalPosition;
      gunTransform.localRotation = GunLocalRotation;
      if (_gun.host) {
        _gun.host.Drop();
        var gunBodies = _gun.host.GetComponentsInChildren<Rigidbody>(true);
        for (var bodyIndex = 0; bodyIndex < gunBodies.Length; bodyIndex++) {
          gunBodies[bodyIndex].velocity = Vector3.zero;
          gunBodies[bodyIndex].angularVelocity = Vector3.zero;
          gunBodies[bodyIndex].useGravity = false;
          gunBodies[bodyIndex].isKinematic = true;
        }
        _gun.host.DisableInteraction();
        _gun.host.DisableColliders();
      }
    }

    var magazines = _gun.GetComponentsInChildren<Magazine>(true);
    if (magazines.Length > 0)
      _magazine = magazines[0];
    if (!_magazine && _gun.spawnableMagazine) {
      var magazineObject = PoolManager.Spawn(
          _gun.spawnableMagazine.title, _gun.magazineSocket.transform.position,
          _gun.magazineSocket.transform.rotation,
          new Sst.Utilities.Il2CppNullable<bool>(null)
      );
      if (magazineObject)
        _magazine = magazineObject.GetComponent<Magazine>();
    }
    if (!_magazine) {
      WarnOnce("Waiting for the captured M16 magazine hierarchy.");
      return false;
    }

    RestoreGlitchedMagazinePose();
    Physics.SyncTransforms();
    var positionedGunTransform = _gun.host
        ? _gun.host.transform
        : _gun.transform;
    if (!SeatMagazineAgainstChest(positionedGunTransform)) {
      WarnOnce("Could not seat the glitched magazine against the chest.");
      return false;
    }
    var chestDistance = Vector3.Distance(
        _magazine.transform.position, _rig.physicsRig.m_chest.position
    );
    if (chestDistance > 2f) {
      WarnOnce(
          "Captured gun setup is invalid: magazine is " +
          chestDistance.ToString("0.###") + " m from the chest."
      );
      return false;
    }
    if (!LockFootball())
      return false;
    IsReady = true;
    MelonLogger.Msg(
        "Automated Runoff benchmark ready. Teleported once, restored the " +
        "glitched M16, and locked the foot ball."
    );
    return true;
  }

  private void TeleportPlayer() {
    if (!_rig)
      return;
    var teleported = false;
    try {
      teleported = _rig.Teleport(PlayerPosition, true);
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Benchmark RigManager.Teleport failed: " + exception.Message
      );
    }
    if (!teleported && _rig.gameWorldSkeletonRig)
      _rig.gameWorldSkeletonRig.transform.position = PlayerPosition;
  }

  private bool LockFootball() {
    var physBody = _rig && _rig.physicsRig ? _rig.physicsRig.physBody : null;
    if (!physBody || !physBody._football) {
      WarnOnce("Waiting for the physics-rig foot ball.");
      return false;
    }
    var body = physBody._football.attachedRigidbody;
    if (!body)
      body = physBody.rbFeet;
    if (!body) {
      WarnOnce("The physics-rig foot ball has no rigidbody.");
      return false;
    }
    if (_footballBody != body) {
      UnlockFootball();
      _footballBody = body;
      _originalFootballConstraints = body.constraints;
    }
    body.velocity = Vector3.zero;
    body.angularVelocity = Vector3.zero;
    body.constraints = RigidbodyConstraints.FreezeAll;
    return true;
  }

  private void UnlockFootball() {
    if (!_footballBody)
      return;
    _footballBody.constraints = _originalFootballConstraints;
    _footballBody = null;
  }

  private static Gun FindAnyM16() {
    var guns = UnityEngine.Object.FindObjectsOfType<Gun>();
    for (var i = 0; i < guns.Length; i++) {
      if (guns[i].name.IndexOf("M16", StringComparison.OrdinalIgnoreCase) >= 0)
        return guns[i];
    }
    return null;
  }

  private static Gun SpawnM16() {
    foreach (var spawnable in PoolManager._registeredSpawnableObjects.Values) {
      var title = spawnable.title ?? "";
      var prefabName = spawnable.prefab ? spawnable.prefab.name : "";
      if (title.IndexOf("M16", StringComparison.OrdinalIgnoreCase) < 0 &&
          prefabName.IndexOf("M16", StringComparison.OrdinalIgnoreCase) < 0)
        continue;
      var spawned = PoolManager.Spawn(
          title, PlayerPosition + Vector3.up, Quaternion.identity,
          new Sst.Utilities.Il2CppNullable<bool>(null)
      );
      if (spawned) {
        MelonLogger.Msg("Automated benchmark spawned " + title + ".");
        return spawned.GetComponent<Gun>();
      }
    }
    return null;
  }

  private void RestoreGlitchedMagazinePose() {
    if (!_gun || !_magazine)
      return;
    _magazine.transform.SetParent(_gun.magazineSocket.transform, false);
    _magazine.transform.localPosition = MagazineLocalPosition;
    _magazine.transform.localRotation = MagazineLocalRotation;
    _magazine.isMagazineInserted = true;
    var magazineBodies = _magazine.GetComponentsInChildren<Rigidbody>(true);
    for (var i = 0; i < magazineBodies.Length; i++) {
      magazineBodies[i].velocity = Vector3.zero;
      magazineBodies[i].angularVelocity = Vector3.zero;
      magazineBodies[i].useGravity = false;
      magazineBodies[i].isKinematic = true;
    }
    if (_gun.magazineSocket._magazinePlug)
      _gun.magazineSocket._magazinePlug.magazine = null;
    var colliders = _magazine.GetComponentsInChildren<Collider>(true);
    for (var i = 0; i < colliders.Length; i++)
      colliders[i].enabled = true;
  }

  private bool SeatMagazineAgainstChest(Transform gunTransform) {
    if (!gunTransform || !_rig || !_rig.physicsRig ||
        !_rig.physicsRig.m_chest)
      return false;
    var magazineColliders = _magazine.GetComponentsInChildren<Collider>(true);
    var chestColliders = _rig.physicsRig.m_chest.GetComponents<Collider>();
    Collider nearestMagazine = null;
    Collider nearestChest = null;
    var nearestSquaredDistance = float.PositiveInfinity;
    for (var i = 0; i < magazineColliders.Length; i++) {
      if (!magazineColliders[i] || !magazineColliders[i].enabled)
        continue;
      for (var j = 0; j < chestColliders.Length; j++) {
        if (!chestColliders[j] || !chestColliders[j].enabled)
          continue;
        if (IsPenetrating(magazineColliders[i], chestColliders[j]))
          return true;
        var separation = chestColliders[j].bounds.center -
                         magazineColliders[i].bounds.center;
        if (separation.sqrMagnitude >= nearestSquaredDistance)
          continue;
        nearestSquaredDistance = separation.sqrMagnitude;
        nearestMagazine = magazineColliders[i];
        nearestChest = chestColliders[j];
      }
    }
    if (!nearestMagazine || !nearestChest)
      return false;
    var direction = (
        nearestChest.bounds.center - nearestMagazine.bounds.center
    ).normalized;
    if (direction.sqrMagnitude < 0.5f)
      return false;
    for (var step = 0; step < 100; step++) {
      gunTransform.position += direction * 0.005f;
      Physics.SyncTransforms();
      if (!IsPenetrating(nearestMagazine, nearestChest))
        continue;
      gunTransform.position += direction * 0.003f;
      Physics.SyncTransforms();
      return true;
    }
    return false;
  }

  private static bool IsPenetrating(Collider first, Collider second) =>
      Physics.ComputePenetration(
          first, first.transform.position, first.transform.rotation,
          second, second.transform.position, second.transform.rotation,
          out _, out _
      );

  private void WarnOnce(string message) {
    if (_warned)
      return;
    _warned = true;
    MelonLogger.Warning("Automated benchmark: " + message);
  }

}
#endif
