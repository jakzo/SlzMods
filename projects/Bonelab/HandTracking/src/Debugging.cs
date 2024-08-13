using System;
using System.Linq;
using UnityEngine;
using Sst.Utilities;
using SLZ.Rig;

namespace Sst.HandTracking;

public class DebugHand {
  private const float JOINT_SIZE = 0.012f;
  private const float TIP_SIZE = 0.006f;
  private const float BONE_WIDTH = 0.005f;

  private OVRPlugin.Skeleton2 _skeleton;
  private Transform _root;
  private Joint[] _joints;

  public DebugHand(OVRPlugin.Skeleton2 skeleton) { _skeleton = skeleton; }

  public void Update(HandState handState) {
    if (LevelHooks.RigManager == null)
      return;

    if (_root == null || _joints.Any(j => j?.Transform == null))
      CreateVisualization();

    _root.localPosition = handState.Position;
    _root.localRotation = handState.Rotation;
    _root.localScale =
        new Vector3(handState.Scale, handState.Scale, handState.Scale);

    for (var i = 0; i < _joints.Length; i++) {
      var joint = _joints[i];
      var jointState = handState.Joints[i];
      joint.Transform.localRotation = jointState.LocalRotation;
      joint.Line?.SetPositions(new[] {
        joint.Parent.position,
        joint.Transform.position,
      });
    }
  }

  private void CreateVisualization() {
    var size = JOINT_SIZE / 2f;
    var cubePrefab = Geometry.CreatePrefabCube(
        "HandTracking_DebugHand_Joint", Color.red, -size, size, -size, size,
        -size, size
    );
    Geometry.SetMaterial(cubePrefab, Color.red, Shaders.HighlightShader);

    _root = new GameObject("HandTracking_DebugHand_Root").transform;
    var controllerRig =
        LevelHooks.RigManager.controllerRig.Cast<OpenControllerRig>();
    _root.SetParent(controllerRig.vrRoot);

    _joints = new Joint[_skeleton.Bones.Length];
    for (var i = 0; i < _skeleton.Bones.Length; i++) {
      var bone = _skeleton.Bones[i];
      var parent = OVRPlugin.IsValidBone(
                       (OVRPlugin.BoneId)bone.ParentBoneIndex, _skeleton.Type
                   )
          ? _joints[bone.ParentBoneIndex].Transform
          : _root;
      var cube = GameObject.Instantiate(cubePrefab, parent).transform;
      cube.gameObject.SetActive(true);
      cube.localPosition = Utils.FromFlippedZVector3f(bone.Pose.Position);
      cube.localRotation = Utils.FromFlippedZQuatf(bone.Pose.Orientation);

      var boneId = (OVRPlugin.BoneId)i;
      if (IsTip(boneId)) {
        var scale = TIP_SIZE / JOINT_SIZE;
        cube.localScale = new Vector3(scale, scale, scale);
      }

      var joint = new Joint() {
        BoneId = boneId,
        Transform = cube,
        Parent = parent,
      };

      if (parent != _root) {
        var lineRenderer = cube.gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = BONE_WIDTH;
        lineRenderer.endWidth = BONE_WIDTH;
        lineRenderer.startColor = Color.blue;
        lineRenderer.endColor = Color.blue;
        lineRenderer.material = new Material(Shaders.HighlightShader);
        joint.Line = lineRenderer;
      }

      _joints[i] = joint;
    }

    GameObject.Destroy(cubePrefab);
    Dbg.Log("Created hand visualization");
  }

  private bool IsTip(OVRPlugin.BoneId boneId
  ) => boneId == OVRPlugin.BoneId.Hand_ThumbTip
      || boneId == OVRPlugin.BoneId.Hand_IndexTip
      || boneId == OVRPlugin.BoneId.Hand_MiddleTip
      || boneId == OVRPlugin.BoneId.Hand_RingTip
      || boneId == OVRPlugin.BoneId.Hand_PinkyTip;

  private class Joint {
    public OVRPlugin.BoneId BoneId;
    public Transform Transform;
    public Transform Parent;
    public LineRenderer Line;
  }
}
