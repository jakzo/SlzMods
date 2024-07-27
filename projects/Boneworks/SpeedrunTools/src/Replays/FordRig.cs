using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using StressLevelZero.Rig;

namespace Sst.Replays {
class FordRigPrefab {
  public GameObject Root;

  public FordRigPrefab() {
    Root = CreateGameObject("FordRig", null);

    var realtimeSkeleton = CreateGameObject("RealtimeSkeleton", Root);
    var realtimeSkeletonRig =
        realtimeSkeleton.AddComponent<RealtimeSkeletonRig>();

    var gameWorldSkeleton = CreateGameObject("GameWorldSkeleton", Root);
    var gameWorldSkeletonRig =
        gameWorldSkeleton.AddComponent<GameWorldSkeletonRig>();

    var rigManager = Root.AddComponent<RigManager>();
    rigManager.realtimeSkeletonRig = realtimeSkeletonRig;
    rigManager.gameWorldSkeletonRig = gameWorldSkeletonRig;
  }

  private static GameObject CreateGameObject(string name, GameObject parent) {
    var go = new GameObject($"SpeedrunTools_Ghost_{name}") {
      // https://gamedev.stackexchange.com/questions/71713/how-to-create-a-new-gameobject-without-adding-it-to-the-scene
      // hideFlags = HideFlags.HideInHierarchy,
      active = parent != null,
    };
    GameObject.DontDestroyOnLoad(go);
    if (parent != null)
      go.transform.SetParent(parent.transform);
    return go;
  }
}

class FordRig : GhostRig {
  public static FordRigPrefab Prefab = new FordRigPrefab();

  public GameObject _root;
  private Transform _head;
  private Transform _controllerLeft;
  private Transform _controllerRight;
  private GameObject[] _gameObjects;

  public FordRig(Transform parent) {
    _root = GameObject.Instantiate(Prefab.Root, parent);
    _root.active = true;
    _head = _root.transform.Find("SpeedrunTools_Ghost_Head");
    _controllerLeft =
        _root.transform.Find("SpeedrunTools_Ghost_ControllerLeft");
    _controllerRight =
        _root.transform.Find("SpeedrunTools_Ghost_ControllerRight");
    _gameObjects = Utilities.Unity.ChildrenToArray(_root);

    // Set transparent render mode
    foreach (var gameObject in _gameObjects) {
      var material = gameObject.GetComponent<MeshRenderer>().material;
      material.SetFloat("_Mode", 3);
      material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
      material.SetInt(
          "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
      );
      material.SetInt("_ZWrite", 0);
      material.DisableKeyword("_ALPHATEST_ON");
      material.DisableKeyword("_ALPHABLEND_ON");
      material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
      material.renderQueue = 3000;
    }
  }

  public override void SetColor(Color color) {
    foreach (var transform in _gameObjects)
      transform.GetComponent<MeshRenderer>().material.color = color;
  }

  public override void SetVisible(bool isVisible) { _root.active = isVisible; }

  public override void Destroy() { Object.Destroy(_root); }

  public override void SetState(Bwr.Frame frame1, Bwr.Frame frame2, float t) {
    _head.position = Vector3.Lerp(
        GhostRig.ToUnityVec3(frame1.PlayerState.Value.HeadPosition),
        GhostRig.ToUnityVec3(frame2.PlayerState.Value.HeadPosition), t
    );
    _head.rotation = Quaternion.Lerp(
        GhostRig.ToUnityQuaternion(
            frame1.VrInput.Value.Headset.Transform.RotationEuler,
            frame1.PlayerState.Value.RootRotation
        ),
        GhostRig.ToUnityQuaternion(
            frame2.VrInput.Value.Headset.Transform.RotationEuler,
            frame2.PlayerState.Value.RootRotation
        ),
        t
    );
    foreach (var (controller, handCur, handNext) in new[] {
               (_controllerLeft, frame1.PlayerState.Value.LeftHand,
                frame2.PlayerState.Value.LeftHand),
               (_controllerRight, frame1.PlayerState.Value.RightHand,
                frame2.PlayerState.Value.RightHand),
             }) {
      controller.position = Vector3.Lerp(
          GhostRig.ToUnityVec3(handCur.Position),
          GhostRig.ToUnityVec3(handNext.Position), t
      );
      controller.rotation = Quaternion.Lerp(
          GhostRig.ToUnityQuaternion(handCur.RotationEuler),
          GhostRig.ToUnityQuaternion(handNext.RotationEuler), t
      );
    }
  }
}
}
