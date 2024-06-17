using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using System.Reflection;
using System.Linq;

#if ML6
using Il2CppInterop.Runtime;
#else
using UnhollowerRuntimeLib;
using UnhollowerBaseLib;
#endif

namespace Sst.Utilities {
public static class Colliders {
  private static Color _colorHighlighter = new Color(1f, 0f, 0f, 1f);
  private static Color _colorDefault = new Color(1f, 0.25f, 0.25f, 0.05f);

  private static Assembly _unityAssembly;
  public static Assembly UnityAssembly {
    get => _unityAssembly ??
           (_unityAssembly = AppDomain.CurrentDomain.GetAssemblies().First(
                asm => asm.GetName().Name == "UnityEngine"));
  }

  public static int NonPhysicalLayerMask = LayerMask.GetMask(new[] {
    "UI",
    "Trigger",
  });

  public static UnityEngine.Collider
  ToUnderlyingType(UnityEngine.Collider collider) {
    var classPtr = IL2CPP.il2cpp_object_get_class(collider.Pointer);
    Il2CppSystem.Type il2cppType = Il2CppType.TypeFromPointer(classPtr);
    var type = UnityAssembly.GetType(il2cppType.FullName);
    var castMethod = typeof(UnhollowerBaseLib.Il2CppObjectBase)
                         .GetMethod("TryCast")
                         .MakeGenericMethod(type);
    return castMethod.Invoke(collider, null) as UnityEngine.Collider;
  }

  public static bool IsColliderPhysical(UnityEngine.Collider collider) {
    if (!collider.enabled || collider.isTrigger)
      return false;
    if ((collider.gameObject.layer & NonPhysicalLayerMask) != 0)
      return false;
    if (collider.attachedRigidbody) {
      return !collider.attachedRigidbody.isKinematic &&
             collider.attachedRigidbody.detectCollisions;
    }
    return true;
  }

  public static LayerMask DEFAULT_LAYER_MASK =
      new LayerMask() { value = 0x7fffffff };

  public static class DebugColliderPrefabs {
    public static GameObject BOX =
        Geometry.CreatePrefabCube("DebugBoxCollider", Color.magenta, -0.5f,
                                  0.5f, -0.5f, 0.5f, -0.5f, 0.5f);
    public static GameObject SPHERE = Geometry.CreatePrefabSphere(
        "DebugSphereCollider", Color.magenta, 0.5f, 2);
    public static GameObject CYLINDER = Geometry.CreatePrefabUnclosedCylinder(
        "DebugCylinderCollider", Color.magenta, 0.5f, 20, 0.5f, -0.5f);
  }

  public static IEnumerable<Collider> AllColliders() {
    foreach (var rootObject in Utilities.Unity.RootObjects()) {
      var colliders = new List<Collider>();
      Utilities.Unity.FindDescendantComponentsOfType(
          ref colliders, rootObject.transform, true);
      foreach (var unknownCollider in colliders) {
        yield return Utilities.Colliders.ToUnderlyingType(unknownCollider);
      }
    }
  }

  public static void VisualizeAllIn(GameObject ancestor,
                                    bool visualizeTriggers = false) =>
      VisualizeAllIn(ancestor, DEFAULT_LAYER_MASK, visualizeTriggers);
  public static void VisualizeAllIn(GameObject ancestor, LayerMask layerMask,
                                    bool visualizeTriggers = false) {
    DebugColliderPrefabs.BOX =
        Geometry.CreatePrefabCube("DebugBoxCollider", Color.magenta, -0.5f,
                                  0.5f, -0.5f, 0.5f, -0.5f, 0.5f);
    DebugColliderPrefabs.SPHERE = Geometry.CreatePrefabSphere(
        "DebugSphereCollider", Color.magenta, 0.5f, 2);
    DebugColliderPrefabs.CYLINDER = Geometry.CreatePrefabUnclosedCylinder(
        "DebugCylinderCollider", Color.magenta, 0.5f, 20, 0.5f, -0.5f);

    var colliders = new List<Collider>();
    Unity.FindDescendantComponentsOfType(ref colliders, ancestor.transform,
                                         true);
    foreach (var unknownCollider in colliders) {
      var collider = ToUnderlyingType(unknownCollider);
      var isMatchingLayer = (layerMask.value & collider.gameObject.layer) != 0;
      if (!isMatchingLayer)
        continue;
      if (collider.isTrigger ? !visualizeTriggers
                             : !IsColliderPhysical(collider))
        continue;

      // Shader default renders double sided but highlighter does not.
      // Mesh colliders only have collision in the direction of their faces
      // but convex mesh colliders are solid.
      if (collider is MeshCollider && !((MeshCollider)collider).convex) {
        Visualize(collider, _colorHighlighter,
                  Utilities.Shaders.HighlightShader);
      } else {
        Visualize(collider, _colorDefault, Utilities.Shaders.DefaultShader);
      }
    }
  }

  public static GameObject Visualize(Collider collider, Color color,
                                     Shader shader, Transform parent = null) {
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

  [RegisterTypeInIl2Cpp]
  public class ColliderVisualization : MonoBehaviour {
    public UnityEngine.Collider Collider;

    public ColliderVisualization(IntPtr ptr) : base(ptr) {}

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
