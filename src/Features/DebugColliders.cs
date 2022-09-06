using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace SpeedrunTools.Features {
class DebugColliders : Feature {
  // TODO: Be consistent about whether positive Y means up or down
  private static readonly GameObject PREFAB_BOX =
      Utilities.Geometry.CreatePrefabCube("BoxCollider", Color.red, -0.5f, 0.5f,
                                          -0.5f, 0.5f, -0.5f, 0.5f);
  private static readonly GameObject PREFAB_SPHERE =
      Utilities.Geometry.CreatePrefabSphere("SphereCollider", Color.red, 0.5f,
                                            1);
  private static readonly GameObject PREFAB_CYLINDER =
      Utilities.Geometry.CreatePrefabUnclosedCylinder(
          "CylinderCollider", Color.red, 0.5f, 20, 0.5f, -0.5f);

  private HashSet<string> _pressedKeys = new HashSet<string>();
  private List<(Collider, GameObject)> _colliders;

  public DebugColliders() { IsDev = true; }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    var rigManager = Object.FindObjectOfType<StressLevelZero.Rig.RigManager>();
    var colliders = new List<Collider>();
    Utilities.Unity.FindDescendantComponentsOfType(
        ref colliders, rigManager.physicsRig.transform);
    _colliders = new List<(Collider, GameObject)>();
    foreach (var collider in colliders) {
      GameObject visualization = null;

      switch (collider) {
      case BoxCollider boxCollider: {
        visualization =
            GameObject.Instantiate(PREFAB_BOX, collider.gameObject.transform);
        visualization.transform.localPosition = boxCollider.center;
        visualization.transform.localScale = boxCollider.size;
        break;
      }

      case SphereCollider sphereCollider: {
        visualization = GameObject.Instantiate(PREFAB_SPHERE,
                                               collider.gameObject.transform);
        visualization.transform.localPosition = sphereCollider.center;
        var diameter = sphereCollider.radius * 2;
        visualization.transform.localScale =
            new Vector3(diameter, diameter, diameter);
        break;
      }

      case CapsuleCollider capsuleCollider: {
        visualization = new GameObject("SpeedrunTools_Colliders_Capsule");
        GameObject.Instantiate(PREFAB_CYLINDER, visualization.transform);
        var endA =
            GameObject.Instantiate(PREFAB_SPHERE, visualization.transform);
        endA.transform.localPosition = new Vector3(0, -0.5f, 0);
        var endB =
            GameObject.Instantiate(PREFAB_SPHERE, visualization.transform);
        endB.transform.localPosition = new Vector3(0, 0.5f, 0);

        visualization.transform.SetParent(collider.gameObject.transform);
        visualization.transform.localPosition = capsuleCollider.center;
        var diameter = capsuleCollider.radius * 2;
        visualization.transform.localScale =
            new Vector3(diameter, capsuleCollider.height, diameter);
        visualization.transform.localRotation =
            capsuleCollider.direction == 0   ? Quaternion.Euler(0, 0, 90)
            : capsuleCollider.direction == 2 ? Quaternion.Euler(90, 0, 0)
                                             : Quaternion.identity;
        break;
      }

      default:
        MelonLogger.Warning(
            $"Unsupported collider {collider} with name '{collider.gameObject.name}'. Skipping.");
        break;
      }

      if (visualization != null)
        _colliders.Add((collider, visualization));
    }
  }

  public override void OnUpdate() {
    foreach (var (collider, visualization) in _colliders) {
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
        var diameter = capsuleCollider.radius * 2;
        var scale = visualization.transform.localScale;
        visualization.transform.localScale =
            new Vector3(diameter, capsuleCollider.height, diameter);
        visualization.transform.localRotation =
            capsuleCollider.direction == 0   ? Quaternion.Euler(0, 0, 90)
            : capsuleCollider.direction == 2 ? Quaternion.Euler(90, 0, 0)
                                             : Quaternion.identity;
        break;
      }
      }
    }
  }
}
}
