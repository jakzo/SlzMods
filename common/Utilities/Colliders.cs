using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using System.Reflection;
using System.Linq;

#if ML6
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
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
             asm => asm.GetType("UnityEngine.Collider") != null
         ));
  }

  public static int NonPhysicalLayerMask = LayerMask.GetMask(
#if PATCH4
      new[] {
        // "Default",
        // "TransparentFX",
        "Ignore Raycast", "ObserverTrigger", "Water", "UI",
            // "Fixture",
            // "Player",
            "NoCollide",
            // "Dynamic",
            // "EnemyColliders",
            // "Interactable",
            "Decaverse", "Deciverse", "Socket", "Plug",
            // "PlayerAndNpc",
            // "FeetOnly",
            // "NoFootball",
            "EntityTracker", "BeingTracker", "ObserverTracker", "EntityTrigger",
            "BeingTrigger", "Background",
      }
#else
      // Patch 3
      new[] {
        // "Default",
        // "TransparentFX",
        "Ignore Raycast",
        // "Water",
        "UI",
        // "Player",
        "NoCollide",
        // "Dynamic",
        // "SterioRender_Ignore",
        // "EnemyColliders",
        // "Static",
        // "SpawnGunUI",
        // "Interactable",
        // "Hand",
        // "HandOnly",
        "Socket", "Plug", "InteractableOnly",
        // "PlayerAndNpc",
        // "NoSelfCollide",
        // "FeetOnly",
        // "NoFootball",
        "Tracker", "Trigger",
        // "Background",
      }
#endif
  );

  // TODO: I think ML6 has a built-in for this
  public static Collider ToUnderlyingType(Collider collider) {
    var classPtr = IL2CPP.il2cpp_object_get_class(collider.Pointer);
    Il2CppSystem.Type il2cppType = Il2CppType.TypeFromPointer(classPtr);
    var type = UnityAssembly.GetType(il2cppType.FullName);
    var castMethod =
        typeof(Il2CppObjectBase).GetMethod("TryCast").MakeGenericMethod(type);
    return castMethod.Invoke(collider, null) as Collider;
  }

  public static bool IsColliderPhysical(Collider collider) {
    if (collider.isTrigger)
      return false;
    if (((1 << collider.gameObject.layer) & NonPhysicalLayerMask) != 0)
      return false;
    if (collider.attachedRigidbody) {
      return collider.attachedRigidbody.detectCollisions;
    }
    return true;
  }

  public static LayerMask DEFAULT_LAYER_MASK =
      new LayerMask() { value = 0x7fffffff };

  public static class DebugColliderPrefabs {
    public static GameObject BOX = Geometry.CreatePrefabCube(
        "DebugCollider_Box", Color.magenta, -0.5f, 0.5f, -0.5f, 0.5f, -0.5f,
        0.5f
    );
    public static GameObject SPHERE = Geometry.CreatePrefabSphere(
        "DebugCollider_Sphere", Color.magenta, 0.5f, 2
    );
    public static GameObject CYLINDER = Geometry.CreatePrefabUnclosedCylinder(
        "DebugCollider_Cylinder", Color.magenta, 0.5f, 20, 0.5f, -0.5f
    );
  }

  public static IEnumerable<Collider> AllColliders() {
    foreach (var rootObject in Utilities.Unity.RootObjects()) {
      var colliders = new List<Collider>();
      Utilities.Unity.FindDescendantComponentsOfType(
          ref colliders, rootObject.transform, true
      );
      foreach (var unknownCollider in colliders) {
        yield return ToUnderlyingType(unknownCollider);
      }
    }
  }

  public static void VisualizeAllIn(
      GameObject ancestor, bool visualizeTriggers = false
  ) => VisualizeAllIn(ancestor, DEFAULT_LAYER_MASK, visualizeTriggers);
  public static void VisualizeAllIn(
      GameObject ancestor, LayerMask layerMask, bool visualizeTriggers = false
  ) {
    DebugColliderPrefabs.BOX = Geometry.CreatePrefabCube(
        "DebugCollider_Box", Color.magenta, -0.5f, 0.5f, -0.5f, 0.5f, -0.5f,
        0.5f
    );
    DebugColliderPrefabs.SPHERE = Geometry.CreatePrefabSphere(
        "DebugCollider_Sphere", Color.magenta, 0.5f, 2
    );
    DebugColliderPrefabs.CYLINDER = Geometry.CreatePrefabUnclosedCylinder(
        "DebugCollider_Cylinder", Color.magenta, 0.5f, 20, 0.5f, -0.5f
    );

    var colliders = new List<Collider>();
    Unity.FindDescendantComponentsOfType(
        ref colliders, ancestor.transform, true
    );
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
        Visualize(
            collider, _colorHighlighter, Utilities.Shaders.HighlightShader
        );
      } else {
        Visualize(collider, _colorDefault, Utilities.Shaders.DefaultShader);
      }
    }
  }

  public static ColliderVisualization Visualize(
      Collider collider, Color color, Shader shader,
      bool watchForChanges = true,
      Func<ColliderVisualization, bool> onUpdate = null, Transform parent = null
  ) {
    var castedCollider = ToUnderlyingType(collider);

    if (parent == null)
      parent = castedCollider.transform;

    GameObject visualization;
    switch (castedCollider) {
    case BoxCollider boxCollider: {
      visualization = GameObject.Instantiate(DebugColliderPrefabs.BOX, parent);
      Geometry.SetMaterial(visualization, color, shader);
      visualization.active = true;
      break;
    }

    case SphereCollider sphereCollider: {
      visualization =
          GameObject.Instantiate(DebugColliderPrefabs.SPHERE, parent);
      Geometry.SetMaterial(visualization, color, shader);
      visualization.active = true;
      break;
    }

    case CapsuleCollider capsuleCollider: {
      visualization = new GameObject("SpeedrunTools_DebugCollider_Capsule");
      var cylinder = GameObject.Instantiate(
          DebugColliderPrefabs.CYLINDER, visualization.transform
      );
      Geometry.SetMaterial(cylinder, color, shader);
      cylinder.active = true;
      var endA = GameObject.Instantiate(
          DebugColliderPrefabs.SPHERE, visualization.transform
      );
      Geometry.SetMaterial(endA, color, shader);
      endA.active = true;
      endA.transform.localPosition = new Vector3(0, -0.5f, 0);
      var endB = GameObject.Instantiate(
          DebugColliderPrefabs.SPHERE, visualization.transform
      );
      Geometry.SetMaterial(endB, color, shader);
      endB.active = true;
      endB.transform.localPosition = new Vector3(0, 0.5f, 0);
      visualization.transform.SetParent(parent, false);
      break;
    }

    case MeshCollider meshCollider: {
      visualization = new GameObject("SpeedrunTools_DebugCollider_Mesh");
      var meshFilter = visualization.AddComponent<MeshFilter>();
      meshFilter.mesh = meshCollider.sharedMesh;
      var meshRenderer = visualization.AddComponent<MeshRenderer>();
      Geometry.SetMaterial(meshRenderer.gameObject, color, shader);
      visualization.transform.SetParent(parent, false);
      visualization.active = true;
      break;
    }

    default: {
      MelonLogger.Warning(
          $"Cannot render collider of unsupported type: {castedCollider}"
      );
      return null;
    }
    }

    var cv = visualization.AddComponent<ColliderVisualization>();
    cv.Collider = castedCollider;
    cv.Renderers =
        visualization.GetComponents<Renderer>()
            .Concat(visualization.GetComponentsInChildren<Renderer>())
            .ToArray();
    cv.WatchForChanges = watchForChanges;
    cv.OnUpdate = onUpdate;
    cv.UpdateVisualization();
    return cv;
  }

  [RegisterTypeInIl2Cpp]
  public class ColliderVisualization : MonoBehaviour {
    public Collider Collider;
    public Renderer[] Renderers;
    public bool WatchForChanges = false;
    /// Called at UpdateVisualization start and cancels update if returns false
    public Func<ColliderVisualization, bool> OnUpdate;

    private Vector3 _cachedCenter;
    private Vector3 _cachedOther;
    private MeshFilter _meshFilter;

    public ColliderVisualization(IntPtr ptr) : base(ptr) {}

    public void LateUpdate() {
      if (WatchForChanges)
        UpdateVisualization();
    }

    public void UpdateVisualization() {
      if (!(OnUpdate?.Invoke(this) ?? true))
        return;

      switch (Collider) {
      case BoxCollider boxCollider: {
        if (!NeedsUpdate(boxCollider.center, boxCollider.size))
          break;
        transform.localPosition = boxCollider.center;
        transform.localScale = boxCollider.size;
        break;
      }

      case SphereCollider sphereCollider: {
        if (!NeedsUpdate(
                sphereCollider.center,
                new Vector3(sphereCollider.radius, 0f, 0f)
            ))
          break;
        transform.localPosition = sphereCollider.center;
        var diameter = sphereCollider.radius * 2;
        transform.localScale = new Vector3(diameter, diameter, diameter);
        break;
      }

      case CapsuleCollider capsuleCollider: {
        if (!NeedsUpdate(
                capsuleCollider.center,
                new Vector3(
                    capsuleCollider.direction, capsuleCollider.radius,
                    capsuleCollider.height
                )
            ))
          break;
        transform.localPosition = capsuleCollider.center;
        transform.localRotation = capsuleCollider.direction == 0
            ? Quaternion.Euler(0, 0, 90)
            : capsuleCollider.direction == 2 ? Quaternion.Euler(90, 0, 0)
                                             : Quaternion.identity;
        var diameter = capsuleCollider.radius * 2;
        var cylinderHeight = Mathf.Max(0f, capsuleCollider.height - diameter);
        transform.GetChild(0).localScale =
            new Vector3(diameter, cylinderHeight, diameter);
        foreach (var i in new[] { 1, 2 }) {
          var sphere = transform.GetChild(i);
          sphere.localPosition =
              new Vector3(0, cylinderHeight / (i == 1 ? -2 : 2), 0);
          sphere.localScale = new Vector3(diameter, diameter, diameter);
        }
        break;
      }

      case MeshCollider meshCollider: {
        if (_meshFilter == null) {
          _meshFilter = GetComponent<MeshFilter>();
          if (_meshFilter == null)
            break;
        }
        if (meshCollider.sharedMesh != _meshFilter.mesh)
          _meshFilter.mesh = meshCollider.sharedMesh;
        break;
      }
      }
    }

    private bool NeedsUpdate(Vector3 center, Vector3 other) {
      if (center == _cachedCenter && other == _cachedOther)
        return false;

      _cachedCenter = center;
      _cachedOther = other;
      return true;
    }
  }
}
}
