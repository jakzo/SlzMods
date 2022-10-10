using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace Sst.Features {
class DebugColliders : Feature {
  private DebugColliderPrefabs _prefabs;
  private List<(Collider, GameObject)> _colliders;

  public readonly Pref<bool> PrefHideBody = new Pref<bool>() {
    Id = "debugCollidersHideBody",
    Name = "Hides Ford's body when showing colliders",
    DefaultValue = true,
  };

  public DebugColliders() { IsDev = true; }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    if (Mod.GameState.rigManager == null) {
      MelonLogger.Warning("Could not find rig manager for debugging colliders");
      return;
    }

    _prefabs = new DebugColliderPrefabs();
    _colliders = new List<(Collider, GameObject)>();

    CreateVisualizationFromCollider<BoxCollider>(collider => {
      var visualization =
          GameObject.Instantiate(_prefabs.BOX, collider.gameObject.transform);
      visualization.active = true;
      return visualization;
    });

    CreateVisualizationFromCollider<SphereCollider>(collider => {
      var visualization = GameObject.Instantiate(_prefabs.SPHERE,
                                                 collider.gameObject.transform);
      visualization.active = true;
      return visualization;
    });

    CreateVisualizationFromCollider<CapsuleCollider>(collider => {
      var visualization = new GameObject("SpeedrunTools_Colliders_Capsule");
      var cylinder =
          GameObject.Instantiate(_prefabs.CYLINDER, visualization.transform);
      cylinder.active = true;
      var endA =
          GameObject.Instantiate(_prefabs.SPHERE, visualization.transform);
      endA.active = true;
      endA.transform.localPosition = new Vector3(0, -0.5f, 0);
      var endB =
          GameObject.Instantiate(_prefabs.SPHERE, visualization.transform);
      endB.active = true;
      endB.transform.localPosition = new Vector3(0, 0.5f, 0);
      visualization.transform.SetParent(collider.gameObject.transform);
      return visualization;
    });

    if (PrefHideBody.Read())
      foreach (var name in new[] { "Brett@neutral", "Body" }) {
        var transform =
            Mod.GameState.rigManager.gameWorldSkeletonRig.transform.FindChild(
                name);
        if (transform != null)
          transform.gameObject.active = false;
      }
  }

  public override void OnLoadingScreen(int nextSceneIdx, int prevSceneIdx) {
    _colliders = null;
  }

  public override void OnUpdate() {
    if (_colliders == null)
      return;

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
  }

  private void
  CreateVisualizationFromCollider<T>(System.Func<T, GameObject> action)
      where T : Collider {
    var colliders = new List<T>();
    Utilities.Unity.FindDescendantComponentsOfType(
        ref colliders, Mod.GameState.rigManager.physicsRig.transform);
    var activeColliders = colliders.Where(
        collider => collider.enabled && collider.attachedRigidbody != null &&
                    IsPhysicalLayer(collider.gameObject.layer));
    foreach (var collider in activeColliders) {
      var visualization = action(collider);
      if (visualization != null)
        _colliders.Add((collider, visualization));
    }
  }

  private static readonly int[] PHYSICAL_LAYERS =
      new[] { "Player", "Hand", "Feet" }
          .Select(LayerMask.NameToLayer)
          .ToArray();
  private bool IsPhysicalLayer(int layer) => PHYSICAL_LAYERS.Contains(layer);

  private class DebugColliderPrefabs {
    // TODO: Be consistent about whether positive Y means up or down
    public readonly GameObject BOX = Utilities.Geometry.CreatePrefabCube(
        "BoxCollider", Color.red, -0.5f, 0.5f, -0.5f, 0.5f, -0.5f, 0.5f);
    public readonly GameObject SPHERE = Utilities.Geometry.CreatePrefabSphere(
        "SphereCollider", Color.red, 0.5f, 2);
    public readonly GameObject CYLINDER =
        Utilities.Geometry.CreatePrefabUnclosedCylinder(
            "CylinderCollider", Color.red, 0.5f, 20, 0.5f, -0.5f);
  }
}
}
