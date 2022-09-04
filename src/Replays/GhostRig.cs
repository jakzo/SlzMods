using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpeedrunTools.Replays {
class GhostRigPrefab {
  private const float HEAD_WIDTH = 0.4f;
  private const float HEAD_HEIGHT = 0.25f;
  private const float HEAD_DEPTH = 0.15f;
  private const float CONTROLLER_WIDTH = 0.15f;
  private const float CONTROLLER_HEIGHT = 0.20f;
  private const float CONTROLLER_DEPTH = 0.15f;

  public GameObject Root = CreateHead();
  public GameObject Head = CreateHead();
  public GameObject ControllerLeft = CreateController(true);
  public GameObject ControllerRight = CreateController(false);

  private static GameObject CreateHead() {
    var head = CreateGameObject("Head");
    AddCubeMesh(ref head, HEAD_WIDTH * -0.5f, HEAD_WIDTH * 0.5f,
                HEAD_HEIGHT * -0.5f, HEAD_HEIGHT * 0.5f, HEAD_DEPTH * 0.0f,
                HEAD_DEPTH * 1.0f);
    return head;
  }

  private static GameObject CreateController(bool isLeft) {
    var controller =
        CreateGameObject($"Controller{(isLeft ? "Left" : "Right")}");
    AddCubeMesh(ref controller, CONTROLLER_WIDTH * -0.5f,
                CONTROLLER_WIDTH * 0.5f, CONTROLLER_HEIGHT * -0.4f,
                CONTROLLER_HEIGHT * 0.6f, CONTROLLER_DEPTH * 0.5f,
                CONTROLLER_DEPTH * -0.5f);
    return controller;
  }

  private static GameObject CreateGameObject(string name) {
    var go = new GameObject($"SpeedrunTools_Ghost_{name}") {
      // https://gamedev.stackexchange.com/questions/71713/how-to-create-a-new-gameobject-without-adding-it-to-the-scene
      // hideFlags = HideFlags.HideInHierarchy,
      active = false,
    };
    GameObject.DontDestroyOnLoad(go);
    return go;
  }

  private static void AddCubeMesh(ref GameObject gameObject, float left,
                                  float right, float top, float bottom,
                                  float front, float back) {
    var meshFilter = gameObject.AddComponent<MeshFilter>();
    meshFilter.mesh.Clear();
    meshFilter.mesh.vertices = new[] {
      new Vector3(left, top, back),     new Vector3(right, top, back),
      new Vector3(right, bottom, back), new Vector3(left, bottom, back),
      new Vector3(left, bottom, front), new Vector3(right, bottom, front),
      new Vector3(right, top, front),   new Vector3(left, top, front),
    };
    meshFilter.mesh.triangles = new[] {
      0, 2, 1, 0, 3, 2, // front
      2, 3, 4, 2, 4, 5, // top
      1, 2, 5, 1, 5, 6, // right
      0, 7, 4, 0, 4, 3, // left
      5, 4, 7, 5, 7, 6, // back
      0, 6, 7, 0, 1, 6, // bottom
    };
    meshFilter.mesh.Optimize();
    meshFilter.mesh.RecalculateNormals();
    gameObject.AddComponent<MeshRenderer>();
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
    foreach (var transform in new Transform[] { Head, ControllerLeft,
                                                ControllerRight })
      transform.gameObject.GetComponent<MeshRenderer>().material.color = color;
  }
}
}
