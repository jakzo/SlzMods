using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Sst.Replays {
class BlockRigPrefab {
  private const float HEAD_WIDTH = 0.3f;
  private const float HEAD_HEIGHT = 0.2f;
  private const float HEAD_DEPTH = 0.1f;
  private const float CONTROLLER_WIDTH = 0.10f;
  private const float CONTROLLER_HEIGHT = 0.15f;
  private const float CONTROLLER_DEPTH = 0.10f;

  public GameObject Root;
  public GameObject Head;
  public GameObject ControllerLeft;
  public GameObject ControllerRight;

  public BlockRigPrefab() {
    Root = CreateGameObject("Root", null);
    Head = CreateHead(Root);
    ControllerLeft = CreateController(Root, true);
    ControllerRight = CreateController(Root, false);
  }

  private static GameObject CreateHead(GameObject root) {
    var head = CreateGameObject("Head", root);
    Utilities.Geometry.AddCubeMesh(
        ref head, HEAD_WIDTH * -0.5f, HEAD_WIDTH * 0.5f, HEAD_HEIGHT * -0.5f,
        HEAD_HEIGHT * 0.5f, HEAD_DEPTH * -1.0f, HEAD_DEPTH * 0.0f
    );
    return head;
  }

  private static GameObject CreateController(GameObject root, bool isLeft) {
    var controller =
        CreateGameObject($"Controller{(isLeft ? "Left" : "Right")}", root);
    Utilities.Geometry.AddCubeMesh(
        ref controller, CONTROLLER_WIDTH * -0.5f, CONTROLLER_WIDTH * 0.5f,
        CONTROLLER_HEIGHT * -0.4f, CONTROLLER_HEIGHT * 0.6f,
        CONTROLLER_DEPTH * -0.5f, CONTROLLER_DEPTH * 0.5f
    );
    return controller;
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

class BlockRig : GhostRig {
  public static BlockRigPrefab Prefab = new BlockRigPrefab();

  public GameObject _root;
  private Transform _head;
  private Transform _controllerLeft;
  private Transform _controllerRight;
  private GameObject[] _gameObjects;

  public BlockRig(Transform parent) {
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
