using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpeedrunTools.Replays {
class GhostRigPrefab {
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

  public GhostRigPrefab() {
    Root = CreateGameObject("Root", null);
    Head = CreateHead(Root);
    ControllerLeft = CreateController(Root, true);
    ControllerRight = CreateController(Root, false);
  }

  private static GameObject CreateHead(GameObject root) {
    var head = CreateGameObject("Head", root);
    Utilities.Geometry.AddCubeMesh(
        ref head, HEAD_WIDTH * -0.5f, HEAD_WIDTH * 0.5f, HEAD_HEIGHT * -0.5f,
        HEAD_HEIGHT * 0.5f, HEAD_DEPTH * -1.0f, HEAD_DEPTH * 0.0f);
    return head;
  }

  private static GameObject CreateController(GameObject root, bool isLeft) {
    var controller =
        CreateGameObject($"Controller{(isLeft ? "Left" : "Right")}", root);
    Utilities.Geometry.AddCubeMesh(
        ref controller, CONTROLLER_WIDTH * -0.5f, CONTROLLER_WIDTH * 0.5f,
        CONTROLLER_HEIGHT * -0.4f, CONTROLLER_HEIGHT * 0.6f,
        CONTROLLER_DEPTH * -0.5f, CONTROLLER_DEPTH * 0.5f);
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

class GhostRig {
  public static GhostRigPrefab Prefab = new GhostRigPrefab();

  public GameObject Root;
  public Transform Head;
  public Transform ControllerLeft;
  public Transform ControllerRight;

  public static GhostRig Create(Transform parent, Color color) {
    var root = GameObject.Instantiate(Prefab.Root, parent);
    root.active = true;
    var rig = new GhostRig() {
      Root = root,
      Head = root.transform.Find("SpeedrunTools_Ghost_Head"),
      ControllerLeft =
          root.transform.Find("SpeedrunTools_Ghost_ControllerLeft"),
      ControllerRight =
          root.transform.Find("SpeedrunTools_Ghost_ControllerRight"),
    };
    rig.SetColor(color);
    return rig;
  }

  public void SetColor(Color color) {
    foreach (var transform in new[] { Head, ControllerLeft, ControllerRight }) {
      var material = transform.gameObject.GetComponent<MeshRenderer>().material;

      // Set transparent render mode
      material.SetFloat("_Mode", 3);
      material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
      material.SetInt("_DstBlend",
                      (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
      material.SetInt("_ZWrite", 0);
      material.DisableKeyword("_ALPHATEST_ON");
      material.DisableKeyword("_ALPHABLEND_ON");
      material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
      material.renderQueue = 3000;
      material.color = color;
    }
  }
}
}
