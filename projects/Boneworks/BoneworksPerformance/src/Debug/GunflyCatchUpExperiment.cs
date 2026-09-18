#if DEBUG
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using HarmonyLib;
using MelonLoader;
using StressLevelZero.Rig;
using UnityEngine;

namespace Sst.BoneworksPerformance;

internal sealed class GunflyCatchUpExperiment {
  private const float WarmupSeconds = 2f;
  private const float MeasureSeconds = 6f;
  private const float StallIntervalSeconds = 0.35f;
  private const int StallMilliseconds = 45;
  private const int MaximumSamples = 32768;

  private static readonly bool[] TreatmentSequence = {
    false, true, true, false,
  };

  private readonly bool _forceEnabled;
  private readonly FixedSample[] _samples = new FixedSample[MaximumSamples];
  private AutomatedRunoffBenchmark _benchmark;
  private Collider[] _magazineColliders;
  private Collider[] _chestColliders;
  private Transform _magazineTransform;
  private Transform _gunTransform;
  private Transform _chestTransform;
  private Transform _weaponHostTransform;
  private Rigidbody _pelvisRigidbody;
  private Vector3 _hostPositionInChest;
  private Quaternion _hostRotationInChest;
  private Vector3 _hostLocalPosition;
  private Quaternion _hostLocalRotation;
  private bool _hostPoseOverridden;
  private Vector3 _motionDirection;
  private Vector3 _syntheticOffset;
  private int _motionSign = 1;
  private string _outputDirectory;
  private int _conditionIndex;
  private int _sampleCount;
  private int _lastFixedFrame = -1;
  private int _fixedTickInFrame;
  private float _phaseEndsAt;
  private float _nextStallAt;
  private float _quitAt = -1f;
  private bool _running;
  private bool _measuring;
  private bool _treatment;
  private bool _finished;
  private bool _recorderFailed;

  public GunflyCatchUpExperiment(
      AutomatedRunoffBenchmark benchmark, bool forceEnabled
  ) {
    _benchmark = benchmark;
    _forceEnabled = forceEnabled;
  }

  public void OnSceneWasInitialized(int buildIndex) {
    _lastFixedFrame = -1;
    _fixedTickInFrame = 0;
    _magazineColliders = null;
    _chestColliders = null;
    _magazineTransform = null;
    _gunTransform = null;
    _chestTransform = null;
    _weaponHostTransform = null;
    _pelvisRigidbody = null;
    _syntheticOffset = Vector3.zero;
    _motionSign = 1;
    _running = false;
    _measuring = false;
    _treatment = false;
    _hostPoseOverridden = false;
    _recorderFailed = false;
  }

  public void OnUpdate() {
    if (!_forceEnabled || _finished)
      return;
    var now = Time.realtimeSinceStartup;
    if (_quitAt >= 0f) {
      if (now >= _quitAt)
        Application.Quit();
      return;
    }
    if (!_benchmark.IsReady)
      return;
    if (!_running) {
      if (!TryCacheTargets())
        return;
      StartRun(now);
    }

    if (now >= _nextStallAt) {
      _nextStallAt = now + StallIntervalSeconds;
      Thread.Sleep(StallMilliseconds);
    }

    if (now < _phaseEndsAt)
      return;
    if (!_measuring) {
      RestoreWeaponLocalPose();
      if (!_benchmark.EnsureMagazineChestContact()) {
        MelonLogger.Error(
            "Gun-fly catch-up A/B could not establish contact before " +
            "condition " + (_conditionIndex + 1) + "."
        );
        _finished = true;
        Application.Quit();
        return;
      }
      CaptureWeaponAnchor();
      _measuring = true;
      _sampleCount = 0;
      _phaseEndsAt = now + MeasureSeconds;
      MelonLogger.Msg(
          "Gun-fly catch-up A/B measuring condition " +
          (_conditionIndex + 1) + "/" + TreatmentSequence.Length +
          ": " + ConditionName + "."
      );
      return;
    }

    WriteCondition();
    _conditionIndex++;
    if (_conditionIndex >= TreatmentSequence.Length) {
      WriteSummary();
      _running = false;
      _finished = true;
      RestoreWeaponLocalPose();
      _treatment = false;
      _quitAt = now + 1f;
      MelonLogger.Msg(
          "Gun-fly catch-up A/B complete: " + _outputDirectory
      );
      return;
    }
    PrepareCondition(now);
  }

  public void OnRigManagerFixedUpdatePrefix(RigManager rig) {
    if (!_running || !_benchmark.IsReady || rig != _benchmark.Rig)
      return;

    var frame = Time.frameCount;
    if (frame != _lastFixedFrame) {
      _lastFixedFrame = frame;
      _fixedTickInFrame = 0;
    } else {
      _fixedTickInFrame++;
    }

    try {
      var treatmentMicroseconds = 0.0;
      var treatmentRan = _treatment && _fixedTickInFrame > 0;
      if (treatmentRan) {
        var started = Stopwatch.GetTimestamp();
        ApplyWeaponHostCatchUp();
        treatmentMicroseconds = ElapsedMicroseconds(started);
      }
      if (_measuring && !_recorderFailed)
        RecordFixedSample(rig, treatmentRan, treatmentMicroseconds);
    } catch (Exception exception) {
      _recorderFailed = true;
      MelonLogger.Error(
          "Gun-fly A/B fixed-tick recorder disabled after an error: " +
          exception
      );
    }
  }

  public void OnRigManagerLateUpdatePrefix(RigManager rig) {
    if (!_running || rig != _benchmark.Rig)
      return;
    RestoreWeaponLocalPose();
  }

  public void OnRigManagerLateUpdatePostfix(RigManager rig) {
    // The experiment holds the condition's initial chest-relative pose fixed.
    // The release feature refreshes its anchor after every normal LateUpdate.
  }

  public void OnRigManagerFixedUpdatePostfix(RigManager rig) {
    if (!_running || !_benchmark.IsReady || rig != _benchmark.Rig)
      return;
    try {
      var delta = _motionDirection * (0.025f * _motionSign);
      _motionSign = -_motionSign;
      ApplySyntheticPlayerTranslation(rig, delta);
      _syntheticOffset += delta;
    } catch (Exception exception) {
      if (_recorderFailed)
        return;
      _recorderFailed = true;
      MelonLogger.Error(
          "Gun-fly A/B synthetic motion disabled after an error: " +
          exception
      );
    }
  }

  private string ConditionName => _treatment ? "Treatment" : "Baseline";

  private void StartRun(float now) {
    var root = Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME, "gunfly-catchup-ab"
    );
    _outputDirectory = Path.Combine(
        root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
    );
    Directory.CreateDirectory(_outputDirectory);
    _conditionIndex = 0;
    _running = true;
    PrepareCondition(now);
  }

  private void PrepareCondition(float now) {
    RestoreWeaponLocalPose();
    ResetSyntheticMotion();
    _treatment = TreatmentSequence[_conditionIndex];
    _measuring = false;
    _sampleCount = 0;
    _lastFixedFrame = -1;
    _fixedTickInFrame = 0;
    _recorderFailed = false;
    _hostPoseOverridden = false;
    _phaseEndsAt = now + WarmupSeconds;
    _nextStallAt = now + 0.2f;
    MelonLogger.Msg(
        "Gun-fly catch-up A/B warming condition " +
        (_conditionIndex + 1) + "/" + TreatmentSequence.Length +
        ": " + ConditionName + "."
    );
  }

  private bool TryCacheTargets() {
    var rig = _benchmark.Rig;
    var gun = _benchmark.Gun;
    var magazine = _benchmark.Magazine;
    if (!rig || !gun || !magazine || !rig.physicsRig ||
        !rig.physicsRig.m_chest || !rig.physicsRig.physBody)
      return false;
    _magazineTransform = magazine.transform;
    _gunTransform = gun.transform;
    _chestTransform = rig.physicsRig.m_chest.transform;
    _weaponHostTransform = gun.host ? gun.host.transform : gun.transform;
    _pelvisRigidbody = rig.physicsRig.physBody.rbPelvis;
    if (!_magazineTransform || !_gunTransform || !_chestTransform ||
        !_weaponHostTransform || !_benchmark.WeaponSlotReceiver)
      return false;
    _magazineColliders = magazine.GetComponentsInChildren<Collider>(true);
    _chestColliders = rig.physicsRig.m_chest.GetComponents<Collider>();
    _motionDirection = (
        _chestTransform.position - _magazineTransform.position
    ).normalized;
    if (_motionDirection.sqrMagnitude < 0.5f)
      _motionDirection = Vector3.forward;
    CaptureWeaponAnchor();
    return _magazineColliders.Length > 0 && _chestColliders.Length > 0;
  }

  private void ResetSyntheticMotion() {
    if (_syntheticOffset.sqrMagnitude > 0f && _benchmark?.Rig)
      ApplySyntheticPlayerTranslation(_benchmark.Rig, -_syntheticOffset);
    _syntheticOffset = Vector3.zero;
    _motionSign = 1;
  }

  private static void ApplySyntheticPlayerTranslation(
      RigManager rig, Vector3 delta
  ) {
    var physics = rig.physicsRig;
    var realtime = rig.realtimeSkeletonRig;
    if (!physics || !realtime)
      return;
    Shift(physics.m_head, delta);
    Shift(physics.m_pelvis, delta);
    Shift(physics.m_leftHand, delta);
    Shift(physics.m_rightHand, delta);
    Shift(physics.m_chest, delta);
    Shift(realtime.m_pelvis, delta);
    if (physics.physBody && physics.physBody.rbFeet)
      physics.physBody.rbFeet.position += delta;
    Physics.SyncTransforms();
  }

  private static void Shift(Transform transform, Vector3 delta) {
    if (transform)
      transform.position += delta;
  }

  private void CaptureWeaponAnchor() {
    if (!_weaponHostTransform || !_chestTransform || _hostPoseOverridden)
      return;
    _hostLocalPosition = _weaponHostTransform.localPosition;
    _hostLocalRotation = _weaponHostTransform.localRotation;
    _hostPositionInChest = _chestTransform.InverseTransformPoint(
        _weaponHostTransform.position
    );
    _hostRotationInChest = Quaternion.Inverse(_chestTransform.rotation) *
                           _weaponHostTransform.rotation;
  }

  private void ApplyWeaponHostCatchUp() {
    if (!_weaponHostTransform || !_chestTransform)
      return;
    _weaponHostTransform.SetPositionAndRotation(
        _chestTransform.TransformPoint(_hostPositionInChest),
        _chestTransform.rotation * _hostRotationInChest
    );
    _hostPoseOverridden = true;
    Physics.SyncTransforms();
  }

  private void RestoreWeaponLocalPose() {
    if (!_hostPoseOverridden || !_weaponHostTransform)
      return;
    _weaponHostTransform.localPosition = _hostLocalPosition;
    _weaponHostTransform.localRotation = _hostLocalRotation;
    _hostPoseOverridden = false;
  }

  private void RecordFixedSample(
      RigManager rig, bool treatmentRan, double treatmentMicroseconds
  ) {
    if (_sampleCount >= _samples.Length)
      return;
    var touching = false;
    var maximumPenetration = 0f;
    for (var i = 0; i < _magazineColliders.Length; i++) {
      var magazineCollider = _magazineColliders[i];
      if (!magazineCollider || !magazineCollider.enabled)
        continue;
      for (var j = 0; j < _chestColliders.Length; j++) {
        var chestCollider = _chestColliders[j];
        if (!chestCollider || !chestCollider.enabled)
          continue;
        if (!Physics.ComputePenetration(
                magazineCollider, magazineCollider.transform.position,
                magazineCollider.transform.rotation,
                chestCollider, chestCollider.transform.position,
                chestCollider.transform.rotation,
                out _, out var distance
            ))
          continue;
        touching = true;
        if (distance > maximumPenetration)
          maximumPenetration = distance;
      }
    }

    var magazinePosition = _magazineTransform.position;
    var gunPosition = _gunTransform.position;
    var chestPosition = _chestTransform.position;
    var pelvisVelocity = _pelvisRigidbody
        ? _pelvisRigidbody.velocity
        : Vector3.zero;
    var expectedHostPosition = _chestTransform.TransformPoint(
        _hostPositionInChest
    );
    _samples[_sampleCount++] = new FixedSample {
      Frame = Time.frameCount,
      TickInFrame = _fixedTickInFrame,
      FixedTime = Time.fixedTime,
      TreatmentRan = treatmentRan,
      TreatmentMicroseconds = treatmentMicroseconds,
      Touching = touching,
      Penetration = maximumPenetration,
      MagazinePosition = magazinePosition,
      GunPosition = gunPosition,
      ChestPosition = chestPosition,
      PelvisVelocity = pelvisVelocity,
      HostCatchUpErrorMm = Vector3.Distance(
          _weaponHostTransform.position, expectedHostPosition
      ) * 1000f,
    };
  }

  private void WriteCondition() {
    var path = Path.Combine(
        _outputDirectory,
        "condition-" + (_conditionIndex + 1) + "-" +
        ConditionName.ToLowerInvariant() + ".csv"
    );
    using var writer = new StreamWriter(path, false, Encoding.UTF8);
    writer.WriteLine(
        "frame,tickInFrame,fixedTime,treatmentRan,treatmentUs,touching," +
        "penetration,magX,magY,magZ,gunX,gunY,gunZ,chestX,chestY,chestZ," +
        "pelvisVx,pelvisVy,pelvisVz,hostCatchUpErrorMm"
    );
    for (var i = 0; i < _sampleCount; i++)
      writer.WriteLine(_samples[i].ToCsv());
  }

  private void WriteSummary() {
    var files = Directory.GetFiles(_outputDirectory, "condition-*.csv");
    Array.Sort(files, StringComparer.Ordinal);
    var summary = new StringBuilder();
    summary.AppendLine(
        "condition,mode,samples,catchUpSamples,touchingCatchUp," +
        "touchingCatchUpPercent,averageTreatmentUs,maxTreatmentUs"
    );
    for (var i = 0; i < files.Length; i++) {
      var lines = File.ReadAllLines(files[i]);
      var samples = Math.Max(0, lines.Length - 1);
      var catchUp = 0;
      var touching = 0;
      var treatmentCount = 0;
      var treatmentTotal = 0.0;
      var treatmentMax = 0.0;
      for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) {
        var columns = lines[lineIndex].Split(',');
        if (int.Parse(columns[1], CultureInfo.InvariantCulture) > 0) {
          catchUp++;
          if (bool.Parse(columns[5]))
            touching++;
        }
        if (!bool.Parse(columns[3]))
          continue;
        var cost = double.Parse(columns[4], CultureInfo.InvariantCulture);
        treatmentCount++;
        treatmentTotal += cost;
        if (cost > treatmentMax)
          treatmentMax = cost;
      }
      var percent = catchUp == 0 ? 0.0 : 100.0 * touching / catchUp;
      var averageCost = treatmentCount == 0
          ? 0.0
          : treatmentTotal / treatmentCount;
      var mode = Path.GetFileName(files[i]).Contains("treatment")
          ? "Treatment"
          : "Baseline";
      summary.Append(i + 1).Append(',').Append(mode).Append(',')
          .Append(samples).Append(',').Append(catchUp).Append(',')
          .Append(touching).Append(',').Append(Format(percent)).Append(',')
          .Append(Format(averageCost)).Append(',')
          .AppendLine(Format(treatmentMax));
    }
    File.WriteAllText(
        Path.Combine(_outputDirectory, "summary.csv"), summary.ToString()
    );
  }

  private static double ElapsedMicroseconds(long started) =>
      (Stopwatch.GetTimestamp() - started) * 1000000.0 /
      Stopwatch.Frequency;

  private static string Format(double value) =>
      value.ToString("0.###", CultureInfo.InvariantCulture);

  private struct FixedSample {
    public int Frame;
    public int TickInFrame;
    public float FixedTime;
    public bool TreatmentRan;
    public double TreatmentMicroseconds;
    public bool Touching;
    public float Penetration;
    public Vector3 MagazinePosition;
    public Vector3 GunPosition;
    public Vector3 ChestPosition;
    public Vector3 PelvisVelocity;
    public float HostCatchUpErrorMm;

    public string ToCsv() => string.Join(",", new[] {
      Frame.ToString(CultureInfo.InvariantCulture),
      TickInFrame.ToString(CultureInfo.InvariantCulture),
      FixedTime.ToString("R", CultureInfo.InvariantCulture),
      TreatmentRan.ToString(),
      Format(TreatmentMicroseconds),
      Touching.ToString(),
      Penetration.ToString("R", CultureInfo.InvariantCulture),
      MagazinePosition.x.ToString("R", CultureInfo.InvariantCulture),
      MagazinePosition.y.ToString("R", CultureInfo.InvariantCulture),
      MagazinePosition.z.ToString("R", CultureInfo.InvariantCulture),
      GunPosition.x.ToString("R", CultureInfo.InvariantCulture),
      GunPosition.y.ToString("R", CultureInfo.InvariantCulture),
      GunPosition.z.ToString("R", CultureInfo.InvariantCulture),
      ChestPosition.x.ToString("R", CultureInfo.InvariantCulture),
      ChestPosition.y.ToString("R", CultureInfo.InvariantCulture),
      ChestPosition.z.ToString("R", CultureInfo.InvariantCulture),
      PelvisVelocity.x.ToString("R", CultureInfo.InvariantCulture),
      PelvisVelocity.y.ToString("R", CultureInfo.InvariantCulture),
      PelvisVelocity.z.ToString("R", CultureInfo.InvariantCulture),
      HostCatchUpErrorMm.ToString("R", CultureInfo.InvariantCulture),
    });
  }

  [HarmonyPatch(typeof(RigManager), "FixedUpdate")]
  private static class RigManagerFixedUpdatePatch {
    [HarmonyPrefix]
    private static void Prefix(RigManager __instance) {
      Mod.Instance?.GunflyCatchUp?.OnRigManagerFixedUpdatePrefix(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(RigManager __instance) {
      Mod.Instance?.GunflyCatchUp?.OnRigManagerFixedUpdatePostfix(__instance);
    }
  }

  [HarmonyPatch(typeof(RigManager), "LateUpdate")]
  private static class RigManagerLateUpdatePatch {
    [HarmonyPrefix]
    private static void Prefix(RigManager __instance) {
      Mod.Instance?.GunflyCatchUp?.OnRigManagerLateUpdatePrefix(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(RigManager __instance) {
      Mod.Instance?.GunflyCatchUp?.OnRigManagerLateUpdatePostfix(__instance);
    }
  }
}
#endif
