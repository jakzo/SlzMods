using System;
using System.Linq;
using HarmonyLib;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Warehouse;
using MelonLoader;
using Sst;
using Sst.Utilities;
using UnityEngine;

namespace Jakzo.Testing;

public class RemapSight : Feature<RemapSight> {
  public bool MoveRemapHand;
  public bool DisableEarlyUpdate;
  public int SightVizType = 4;
  public bool AimHand;

  public (Rig Rig, Transform Viz)[] RigViz;
  public Transform SightViz;

  public override void Init() { MelonEvents.OnUpdate.Subscribe(OnUpdate); }

  public override void Deinit() { MelonEvents.OnUpdate.Unsubscribe(OnUpdate); }

  public void OnUpdate() {
    if (LevelHooks.IsLoading)
      return;

    if (SightViz == null)
      CreateViz();

    UpdateSight();
  }

  public void CreateViz() {
    var rm = LevelHooks.RigManager;
    var rigs = new(Rig Rig, Color Color)[] {
      (rm.controllerRig, Color.yellow),
      (rm.remapHeptaRig, Color.red),
      (rm.virtualHeptaRig, Color.green),
      (rm.physicsRig, Color.blue),
    };

    if (rigs.Any(x => x.Rig == null))
      return;

    var size = 0.01f;
    var cubePrefab = Geometry.CreatePrefabCube(
        "viz", Color.red, -size, size, -size, size, -size, size
    );
    Geometry.SetMaterial(cubePrefab, Color.red, Shaders.HighlightShader);

    RigViz =
        rigs.Select(x => {
              var viz =
                  GameObject.Instantiate(cubePrefab, x.Rig.m_handLf).transform;
              Geometry.SetMaterial(
                  viz.gameObject, x.Color, Shaders.HighlightShader
              );
              viz.gameObject.SetActive(true);
              return (x.Rig, viz);
            })
            .ToArray();

    SightViz = GameObject.Instantiate(cubePrefab).transform;
    Geometry.SetMaterial(
        SightViz.gameObject, Color.green, Shaders.HighlightShader
    );
    SightViz.gameObject.SetActive(true);

    Dbg.Log("viz created");
  }

  private const float ROTATION_SIMILARITY_ACTIVATION_THRESHOLD = 0.95f;
  private const float ROTATION_SIMILARITY_DEACTIVATION_THRESHOLD = 0.9f;
  private const float ROTATION_FACTOR = 0.25f;
  private const float POSITION_DAMPING_FACTOR = 0.25f;
  // Sights have a slightly different offset depending on the gun but finding
  // the specific value per gun is a lot of manual effort and won't work for
  // modded guns whereas hardcoding it works well enough
  private static Vector3 SIGHT_OFFSET = new Vector3(0f, 0.03f, 0f);

  public bool IsActive;
  public Quaternion TargetHandRotation;
  public Vector3 DampedVirtualHandPos;

  public void UpdateSight() {
    if (SightViz == null)
      return;

    var physicalHand = LevelHooks.RigManager.physicsRig.leftHand;
    var host = physicalHand?.AttachedReceiver?.TryCast<TargetGrip>()
                   ?.Host?.TryCast<InteractableHost>();
    var gun = host?.GetComponent<Gun>();
    if (gun?.firePointTransform == null || host?.Rb == null)
      return;

    var eye = LevelHooks.RigManager.controllerRig.TryCast<OpenControllerRig>()
                  ?.cameras?[0];
    if (eye == null)
      return;

    var sightRot = gun.firePointTransform.rotation;
    var sightPos = gun.firePointTransform.position + sightRot * SIGHT_OFFSET;

    if (SightVizType == 1) {
      SightViz.position = sightPos;
      SightViz.rotation = sightRot;
    }

    var handToSightPos = physicalHand.jointStartRotation *
        (Quaternion.Inverse(host.Rb.rotation) * (sightPos - host.Rb.position) -
         physicalHand.joint.connectedAnchor + physicalHand.joint.anchor);
    var handToSightRot = sightRot * Quaternion.Inverse(host.Rb.rotation) *
        physicalHand.jointStartRotation;

    if (SightVizType > 2) {
      Rig rig = SightVizType == 3 ? LevelHooks.RigManager.physicsRig
                                  : LevelHooks.RigManager.virtualHeptaRig;
      var h = rig.m_handLf;
      SightViz.position = h.position + h.rotation * handToSightPos;
      SightViz.rotation = h.rotation * handToSightRot;
    }

    var virtualRig = LevelHooks.RigManager.virtualHeptaRig;
    var virtualHand = true ? virtualRig.m_handLf : virtualRig.m_handRt;
    var virtualHandPos = virtualHand.position;
    var virtualHandRot = virtualHand.rotation;

    var sightPosOfHand =
        virtualHand.position + virtualHand.rotation * handToSightPos;
    var sightRotOfHand = virtualHand.rotation * handToSightRot;

    var targetSightRotation =
        Quaternion.LookRotation(sightPosOfHand - eye.transform.position);
    TargetHandRotation =
        targetSightRotation * Quaternion.Inverse(handToSightRot);

    var rotationSimilarity = Quaternion.Dot(TargetHandRotation, virtualHandRot);

    if (IsActive) {
      if (rotationSimilarity < ROTATION_SIMILARITY_DEACTIVATION_THRESHOLD) {
        Dbg.Log("Auto-sight deactivated");
        IsActive = false;
        return;
      }
    } else if (rotationSimilarity >= ROTATION_SIMILARITY_ACTIVATION_THRESHOLD) {
      Dbg.Log("Auto-sight activated");
      IsActive = true;
      DampedVirtualHandPos = virtualHand.localPosition;
    } else {
      return;
    }

    if (AimHand)
      virtualHand.rotation = TargetHandRotation;

    // TODO: Scale up damping factor when delta from last position increases
    var virtualHandPosDelta = virtualHand.localPosition - DampedVirtualHandPos;
    DampedVirtualHandPos += virtualHandPosDelta * POSITION_DAMPING_FACTOR;
    if (AimHand)
      virtualHand.localPosition = DampedVirtualHandPos;

    if (SightVizType == 2) {
      SightViz.position = DampedVirtualHandPos;
      SightViz.rotation = TargetHandRotation;
    }
  }

  [HarmonyPatch(typeof(RemapRig), nameof(RemapRig.OnEarlyUpdate))]
  internal static class RemapRig_OnEarlyUpdate {
    [HarmonyPrefix]
    private static bool Prefix(RemapRig __instance) {
      return !Instance.DisableEarlyUpdate;
    }

    [HarmonyPostfix]
    private static void Postfix(RemapRig __instance) {
      if (!Instance.MoveRemapHand)
        return;

      __instance.m_handLf.localRotation = Quaternion.identity;
      __instance.m_handLf.localPosition =
          new Vector3(Mathf.Sin(Time.time) * 0.4f, 1.2f, 0.3f);
    }
  }
}
