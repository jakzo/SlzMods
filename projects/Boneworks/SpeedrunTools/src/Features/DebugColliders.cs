using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using StressLevelZero.Props.Weapons;
using UnityEngine;
using Valve.VR;

namespace Sst.Features {
class DebugColliders : Feature {
  public static DebugColliders Instance;

  private DebugColliderPrefabs _prefabs;
  private List<(Collider, GameObject)> _colliders =
      new List<(Collider, GameObject)>();
  private float _physicsRate = 0f;

  public readonly Pref<bool> PrefHideBody = new Pref<bool>() {
    Id = "debugCollidersHideBody",
    Name = "Hides things with colliders being visualized",
    DefaultValue = true,
  };

  public DebugColliders() {
    Instance = this;
    IsDev = true;

    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Mod.GameState.rigManager != null &&
                              Utils.GetKeyControl() &&
                              Input.GetKey(KeyCode.Alpha1),
      Handler = () => ShowRigColliders(true),
    });
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Mod.GameState.rigManager != null &&
                              Utils.GetKeyControl() &&
                              Input.GetKey(KeyCode.Alpha2),
      Handler = ShowHeldMagColliders,
    });
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Mod.GameState.rigManager != null &&
                              Utils.GetKeyControl() &&
                              Input.GetKey(KeyCode.Alpha3),
      Handler = ToggleCameraLockedToKnee,
    });
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Mod.GameState.rigManager != null &&
                              Utils.GetKeyControl() &&
                              Input.GetKey(KeyCode.Alpha4),
      Handler = () => Time.timeScale = Time.timeScale == 0f ? 1f : 0f,
    });
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) => Mod.GameState.rigManager != null &&
                              Utils.GetKeyControl() &&
                              Input.GetKey(KeyCode.Alpha5),
      // Pimax Reality 12k QLED
      Handler = () => SetPhysicsRate(_physicsRate == 0f ? 200f : 0f),
    });
  }

  public void SetPhysicsRate(float rate) {

    var steamSettings = Resources.Load<SteamVR_Settings>("SteamVR_Settings");
    steamSettings.lockPhysicsUpdateRateToRenderFrequency = rate == 0f;
    _physicsRate = rate;
  }

  public override void OnLateUpdate() {
    if (_physicsRate != 0f)
      Time.fixedDeltaTime = Time.timeScale / _physicsRate;
  }

  public void ToggleCameraLockedToKnee() {
    var knee = Mod.GameState.rigManager.physicsRig.physBody.knee;
    var cam = GameObject.Find("Camera (Freezes With Preview) [0]");
    cam.transform.SetParent(cam.transform.parent ? null : knee.transform);

    var preview = cam.transform.Find("ScaleFix").Find("Plane");
    preview.transform.SetParent(
        Mod.GameState.rigManager.ControllerRig.rightController.transform);
    preview.transform.localPosition = new Vector3(0f, 0f, 0.05f);
    preview.transform.localRotation = Quaternion.Euler(45f, 160f, 340f);
    Component.Destroy(preview.GetComponent<Collider>());
  }

  // Sst.Features.DebugColliders.Instance.ShowRigColliders(true)
  public void ShowRigColliders(bool transparent = false) {
    if (Mod.GameState.rigManager == null) {
      MelonLogger.Warning("Could not find rig manager for debugging colliders");
      return;
    }

    _prefabs = new DebugColliderPrefabs();

    var transparentShader = Shader.Find("SLZ/Highlighter");

    CreateVisualizationFromCollider<BoxCollider>(collider => {
      var visualization =
          GameObject.Instantiate(_prefabs.BOX, collider.gameObject.transform);
      visualization.active = true;
      if (transparent)
        SetShader(visualization, transparentShader);
      return visualization;
    }, Mod.GameState.rigManager.physicsRig.transform);

    CreateVisualizationFromCollider<SphereCollider>(collider => {
      var visualization = GameObject.Instantiate(_prefabs.SPHERE,
                                                 collider.gameObject.transform);
      visualization.active = true;
      if (transparent)
        SetShader(visualization, transparentShader);
      return visualization;
    }, Mod.GameState.rigManager.physicsRig.transform);

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
      if (transparent) {
        SetShader(cylinder, transparentShader);
        SetShader(endA, transparentShader);
        SetShader(endB, transparentShader);
      }
      return visualization;
    }, Mod.GameState.rigManager.physicsRig.transform);

    if (PrefHideBody.Read())
      DisableRenderers(Mod.GameState.rigManager.gameWorldSkeletonRig.transform);
  }

  public override void OnLoadingScreen(int nextSceneIdx, int prevSceneIdx) {
    _colliders.Clear();
  }

  public override void OnUpdate() {
    foreach (var (collider, visualization) in _colliders) {
      if (!collider)
        continue;

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
  CreateVisualizationFromCollider<T>(System.Func<T, GameObject> action,
                                     Transform parent)
      where T : Collider {
    var colliders = new List<T>();
    Utilities.Unity.FindDescendantComponentsOfType(ref colliders, parent);
    var activeColliders = colliders.Where(
        collider => collider.enabled && collider.attachedRigidbody != null &&
                    !collider.isTrigger &&
                    IsPhysicalLayer(collider.gameObject.layer));
    foreach (var collider in activeColliders) {
      var visualization = action(collider);
      if (visualization != null)
        _colliders.Add((collider, visualization));
    }
  }

  private static readonly int[] PHYSICAL_LAYERS =
      new[] { "Player", "Hand", "Feet", "Dynamic" }
          .Select(LayerMask.NameToLayer)
          .ToArray();
  private bool IsPhysicalLayer(int layer) => PHYSICAL_LAYERS.Contains(layer);

  private static void SetShader(GameObject gameObject, Shader shader = null) {
    var meshRenderer = gameObject.GetComponent<MeshRenderer>();
    meshRenderer.shadowCastingMode =
        UnityEngine.Rendering.ShadowCastingMode.Off;
    meshRenderer.receiveShadows = false;
    var material = meshRenderer.material;
    if (shader != null)
      material.shader = shader;
  }

  // Sst.Features.DebugColliders.Instance.ShowHeldMagColliders()
  public void ShowHeldMagColliders() {
    var gun = Mod.GameState.rigManager.physicsRig.rightHand
                  .m_CurrentAttachedObject?.transform.parent.gameObject;
    if (!gun) {
      MelonLogger.Warning("No gun currently held in right hand");
      return;
    }
    var mag = FindMag(gun);
    if (!mag) {
      MelonLogger.Warning("No glitched mag found");
      return;
    }

    if (PrefHideBody.Read())
      DisableRenderers(gun.transform);

    _prefabs = new DebugColliderPrefabs();
    CreateVisualizationFromCollider<BoxCollider>(collider => {
      var visualization =
          GameObject.Instantiate(_prefabs.BOX, collider.gameObject.transform);
      visualization.active = true;
      visualization.GetComponent<MeshRenderer>().material.color = Color.green;
      return visualization;
    }, mag.transform);
  }

  private void DisableRenderers(Transform parent) {
    // DisableRenderersInternal<MeshRenderer>(parent);
    // DisableRenderersInternal<SkinnedMeshRenderer>(parent);

    var renderers = new List<Renderer>();
    Utilities.Unity.FindDescendantComponentsOfType(ref renderers, parent);
    foreach (var renderer in renderers)
      renderer.enabled = false;
  }
  private void DisableRenderersInternal<T>(Transform parent)
      where T : Renderer {
    var meshRenderers = new List<T>();
    Utilities.Unity.FindDescendantComponentsOfType(ref meshRenderers, parent);
    foreach (var renderer in meshRenderers)
      renderer.enabled = false;
  }

  private GameObject FindMag(GameObject gun) {
    var potentialMags = Utilities.Unity.ChildrenToArray(
        gun.GetComponent<Gun>().magazineSocket.gameObject);
    foreach (var go in potentialMags) {
      if (go.GetComponent<Magazine>())
        return go;
    }
    return null;
  }

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
