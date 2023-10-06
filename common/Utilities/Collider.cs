using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Sst.Utilities {
static class Collider {
  public static LayerMask DEFAULT_LAYER_MASK =
      new LayerMask() { value = 0x7fffffff };

  private static class DebugColliderPrefabs {
    public static GameObject BOX =
        Geometry.CreatePrefabCube("DebugBoxCollider", Color.magenta, -0.5f,
                                  0.5f, -0.5f, 0.5f, -0.5f, 0.5f);
    public static GameObject SPHERE = Geometry.CreatePrefabSphere(
        "DebugSphereCollider", Color.magenta, 0.5f, 2);
    public static GameObject CYLINDER = Geometry.CreatePrefabUnclosedCylinder(
        "DebugCylinderCollider", Color.magenta, 0.5f, 20, 0.5f, -0.5f);
  }

  public static void VisualizeAllIn(GameObject ancestor, Shader shader = null,
                                    bool visualizeTriggers = false) =>
      VisualizeAllIn(ancestor, Color.red, DEFAULT_LAYER_MASK, shader,
                     visualizeTriggers);
  public static void VisualizeAllIn(GameObject ancestor, Color color,
                                    Shader shader = null,
                                    bool visualizeTriggers = false) =>
      VisualizeAllIn(ancestor, color, DEFAULT_LAYER_MASK, shader,
                     visualizeTriggers);
  public static void VisualizeAllIn(GameObject ancestor, Color color,
                                    LayerMask layerMask, Shader shader = null,
                                    bool visualizeTriggers = false) {
    DebugColliderPrefabs.BOX =
        Geometry.CreatePrefabCube("DebugBoxCollider", Color.magenta, -0.5f,
                                  0.5f, -0.5f, 0.5f, -0.5f, 0.5f);
    DebugColliderPrefabs.SPHERE = Geometry.CreatePrefabSphere(
        "DebugSphereCollider", Color.magenta, 0.5f, 2);
    DebugColliderPrefabs.CYLINDER = Geometry.CreatePrefabUnclosedCylinder(
        "DebugCylinderCollider", Color.magenta, 0.5f, 20, 0.5f, -0.5f);

    VisualizeAllIn<BoxCollider>(ancestor, color, layerMask, shader,
                                visualizeTriggers);
    VisualizeAllIn<SphereCollider>(ancestor, color, layerMask, shader,
                                   visualizeTriggers);
    VisualizeAllIn<CapsuleCollider>(ancestor, color, layerMask, shader,
                                    visualizeTriggers);
    VisualizeAllIn<MeshCollider>(ancestor, color, layerMask, shader,
                                 visualizeTriggers);
  }
  public static void VisualizeAllIn<T>(GameObject ancestor, Color color,
                                       LayerMask layerMask,
                                       Shader shader = null,
                                       bool visualizeTriggers = false)
      where T : UnityEngine.Collider {
    var colliders = new List<T>();
    Unity.FindDescendantComponentsOfType(ref colliders, ancestor.transform);
    foreach (var collider in colliders) {
      if ((layerMask.value & collider.gameObject.layer) == 0 ||
          !visualizeTriggers &&
              (collider.isTrigger || !collider.attachedRigidbody))
        continue;
      Visualize(collider, color, shader);
    }
  }

  public static GameObject Visualize(UnityEngine.Collider collider, Color color,
                                     Shader shader = null,
                                     Transform parent = null) {
    GameObject visualization;

    if (parent == null)
      parent = collider.transform;

    switch (collider) {
    case BoxCollider boxCollider: {
      visualization = GameObject.Instantiate(DebugColliderPrefabs.BOX, parent);
      SetMaterial(visualization, color, shader);
      visualization.active = true;
      break;
    }

    case SphereCollider sphereCollider: {
      visualization =
          GameObject.Instantiate(DebugColliderPrefabs.SPHERE, parent);
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
      visualization.transform.SetParent(parent, false);
      break;
    }

    case MeshCollider meshCollider: {
      visualization = new GameObject("DebugMeshCollider");
      var meshFilter = visualization.AddComponent<MeshFilter>();
      meshFilter.mesh = meshCollider.sharedMesh;
      var meshRenderer = visualization.AddComponent<MeshRenderer>();
      SetMaterial(meshRenderer.gameObject, color, shader);
      visualization.transform.SetParent(parent, false);
      visualization.active = true;
      break;
    }

    default: {
      MelonLogger.Warning(
          $"Cannot render collider of unsupported type: {collider}");
      return null;
    }
    }

    var cv = visualization.AddComponent<ColliderVisualization>();
    cv.Collider = collider;
    cv.Update();
    return visualization;
  }

  public class ColliderVisualization : MonoBehaviour {
    public UnityEngine.Collider Collider;

    public void Update() {
      gameObject.active = Collider.gameObject.active && Collider.enabled;

      switch (Collider) {
      case BoxCollider boxCollider: {
        transform.localPosition = boxCollider.center;
        transform.localScale = boxCollider.size;
        break;
      }

      case SphereCollider sphereCollider: {
        transform.localPosition = sphereCollider.center;
        var diameter = sphereCollider.radius * 2;
        transform.localScale = new Vector3(diameter, diameter, diameter);
        break;
      }

      case CapsuleCollider capsuleCollider: {
        transform.localPosition = capsuleCollider.center;
        transform.localRotation =
            capsuleCollider.direction == 0   ? Quaternion.Euler(0, 0, 90)
            : capsuleCollider.direction == 2 ? Quaternion.Euler(90, 0, 0)
                                             : Quaternion.identity;
        var diameter = capsuleCollider.radius * 2;
        transform.GetChild(0).localScale =
            new Vector3(diameter, capsuleCollider.height, diameter);
        foreach (var i in new[] { 1, 2 }) {
          var sphere = transform.GetChild(i);
          sphere.localPosition =
              new Vector3(0, capsuleCollider.height / (i == 1 ? -2 : 2), 0);
          sphere.localScale = new Vector3(diameter, diameter, diameter);
        }
        break;
      }
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
