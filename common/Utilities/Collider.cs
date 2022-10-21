using MelonLoader;
using UnityEngine;

namespace Sst.Utilities {
static class Collider {

  private static class DebugColliderPrefabs {
    public static readonly GameObject BOX = Utilities.Geometry.CreatePrefabCube(
        "DebugBoxCollider", Color.magenta, -0.5f, 0.5f, -0.5f, 0.5f, -0.5f,
        0.5f);
    public static readonly GameObject SPHERE =
        Utilities.Geometry.CreatePrefabSphere("DebugSphereCollider",
                                              Color.magenta, 0.5f, 2);
    public static readonly GameObject CYLINDER =
        Utilities.Geometry.CreatePrefabUnclosedCylinder(
            "DebugCylinderCollider", Color.magenta, 0.5f, 20, 0.5f, -0.5f);
  }

  public static GameObject Visualize(GameObject parent,
                                     UnityEngine.Collider collider, Color color,
                                     Shader shader = null) {
    GameObject visualization;

    switch (collider) {
    case BoxCollider boxCollider: {
      visualization = GameObject.Instantiate(DebugColliderPrefabs.BOX,
                                             collider.gameObject.transform);
      SetMaterial(visualization, color, shader);
      visualization.active = true;
      break;
    }

    case SphereCollider sphereCollider: {
      visualization = GameObject.Instantiate(
          DebugColliderPrefabs.SPHERE, sphereCollider.gameObject.transform);
      SetMaterial(visualization, color, shader);
      visualization.active = true;
      break;
    }

    case CapsuleCollider capsuleCollider: {
      visualization = new GameObject("DebugCapsuleCollider");
      var cylinder = GameObject.Instantiate(DebugColliderPrefabs.CYLINDER,
                                            visualization.transform);
      SetMaterial(cylinder, color, shader);
      cylinder.active = true;
      var endA = GameObject.Instantiate(DebugColliderPrefabs.SPHERE,
                                        visualization.transform);
      SetMaterial(endA, color, shader);
      endA.active = true;
      endA.transform.localPosition = new Vector3(0, -0.5f, 0);
      var endB = GameObject.Instantiate(DebugColliderPrefabs.SPHERE,
                                        visualization.transform);
      SetMaterial(endB, color, shader);
      endB.active = true;
      endB.transform.localPosition = new Vector3(0, 0.5f, 0);
      visualization.transform.SetParent(capsuleCollider.gameObject.transform);
      break;
    }

    default: {
      MelonLogger.Warning(
          $"Cannot render collider of unsupported type: {collider}");
      return null;
    }
    }

    UpdateVisualization(collider, visualization);
    return visualization;
  }

  public static void UpdateVisualization(UnityEngine.Collider collider,
                                         GameObject visualization) {
    switch (collider) {
    case BoxCollider boxCollider: {
      visualization.transform.localPosition = boxCollider.center;
      visualization.transform.localScale = boxCollider.size;
      break;
    }

    case SphereCollider sphereCollider: {
      visualization.transform.localPosition = sphereCollider.center;
      var diameter = sphereCollider.radius * 2;
      visualization.transform.localScale =
          new Vector3(diameter, diameter, diameter);
      break;
    }

    case CapsuleCollider capsuleCollider: {
      visualization.transform.localPosition = capsuleCollider.center;
      visualization.transform.localRotation =
          capsuleCollider.direction == 0   ? Quaternion.Euler(0, 0, 90)
          : capsuleCollider.direction == 2 ? Quaternion.Euler(90, 0, 0)
                                           : Quaternion.identity;
      var diameter = capsuleCollider.radius * 2;
      visualization.transform.GetChild(0).localScale =
          new Vector3(diameter, capsuleCollider.height, diameter);
      foreach (var i in new[] { 1, 2 }) {
        var sphere = visualization.transform.GetChild(i);
        sphere.localPosition =
            new Vector3(0, capsuleCollider.height / (i == 1 ? -2 : 2), 0);
        sphere.localScale = new Vector3(diameter, diameter, diameter);
      }
      break;
    }
    }
  }

  private static void SetMaterial(GameObject gameObject, Color color,
                                  Shader shader = null) {
    var meshRenderer = gameObject.GetComponent<MeshRenderer>();
    meshRenderer.shadowCastingMode =
        UnityEngine.Rendering.ShadowCastingMode.Off;
    meshRenderer.receiveShadows = false;
    var material = meshRenderer.material;
    if (shader != null)
      material.shader = shader;
    material.color = color;
  }
}
}
