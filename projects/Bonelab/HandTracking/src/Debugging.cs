using System;
using System.Linq;
using UnityEngine;
using Sst.Utilities;
using SLZ.Rig;
using TMPro;

namespace Sst.HandTracking;

public class DebugHand {
  private const float JOINT_SIZE = 0.012f;
  private const float TIP_SIZE = 0.006f;
  private const float BONE_WIDTH = 0.005f;

  private static bool LINES_USE_HAND_POS = false;
  private static Vector3 HAND_POS_OFFSET = new Vector3(0f, -0.1f, 0.5f);

  private OVRPlugin.Skeleton2 _skeleton;
  private Transform _root;
  private Joint[] _joints;
  private Transform _marker;

  public DebugHand(OVRPlugin.Skeleton2 skeleton) { _skeleton = skeleton; }

  public void Update(HandState handState) {
    if (!Mod.Preferences.DebugShowHandTracking.Value ||
        LevelHooks.RigManager == null)
      return;

    if (_root == null || _joints.Any(j => j?.Transform == null))
      CreateVisualization();

    _root.localPosition = handState.Position;
    _root.localRotation = handState.Rotation;
    _root.localScale =
        new Vector3(handState.Scale, handState.Scale, handState.Scale);

    var headPosition = LevelHooks.RigManager.physicsRig.m_head.position;
    var offset = headPosition + HAND_POS_OFFSET;

    for (var i = 0; i < _joints.Length; i++) {
      var joint = _joints[i];
      var jointState = handState.Joints[i];
      joint.Transform.localRotation = jointState.LocalRotation;
      if (LINES_USE_HAND_POS) {
        var tracker = _skeleton.Type == OVRPlugin.SkeletonType.HandLeft
            ? Mod.Instance.TrackerLeft
            : Mod.Instance.TrackerRight;
        if (tracker == null)
          continue;

        var parentIdx = _skeleton.Bones[i].ParentBoneIndex;
        joint.Line?.SetPositions(new[] {
          offset + tracker.HandState.Joints[parentIdx].HandPosition,
          offset + tracker.HandState.Joints[i].HandPosition,
        });
      } else {
        joint.Line?.SetPositions(new[] {
          joint.Parent.position,
          joint.Transform.position,
        });

        if (joint.Text != null) {
          joint.Text.SetText(
              i + " " + jointState.LocalRotation.eulerAngles.ToString()
          );
          var positionOffset =
              (headPosition - joint.Transform.position).normalized;
          joint.Text.transform.SetPositionAndRotation(
              joint.Transform.position + positionOffset * JOINT_SIZE,
              Quaternion.LookRotation(-positionOffset)
          );
        }
      }
    }

    if (LINES_USE_HAND_POS) {
      _marker.SetPositionAndRotation(offset, Quaternion.identity);
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
      var joint = new Joint() {
        BoneId = boneId,
        Transform = cube,
        Parent = parent,
      };

      if (IsTip(boneId)) {
        var scale = TIP_SIZE / JOINT_SIZE;
        cube.localScale = new Vector3(scale, scale, scale);
      } else if (Mod.Preferences.DebugShowJointRotations.Value) {
        var text = new GameObject("rotation").AddComponent<TextMeshPro>();
        text.transform.SetParent(cube, false);
        text.fontSize = JOINT_SIZE * 3f;
        text.alignment = TextAlignmentOptions.Center;
        joint.Text = text;
      }

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

    if (LINES_USE_HAND_POS) {
      _marker = GameObject.Instantiate(cubePrefab).transform;
      _marker.gameObject.SetActive(true);
    }

    GameObject.Destroy(cubePrefab);
    Dbg.Log("Created hand visualization");
  }

  private bool IsTip(OVRPlugin.BoneId boneId
  ) => boneId >= OVRPlugin.BoneId.Hand_MaxSkinnable;

  private class Joint {
    public OVRPlugin.BoneId BoneId;
    public Transform Transform;
    public Transform Parent;
    public LineRenderer Line;
    public TextMeshPro Text;
  }
}
