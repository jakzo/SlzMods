using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;

namespace Sst.Utilities {
public static class Unity {
  public static Transform
  FindDescendantTransform(Transform transform, string name) {
    if (transform.name == name)
      return transform;
    for (var i = 0; i < transform.childCount; i++) {
      var child = transform.GetChild(i);
      var result = FindDescendantTransform(child, name);
      if (result != null)
        return result;
    }
    return null;
  }

  public static void FindDescendantComponentsOfType<T>(
      ref List<T> output, Transform parent, bool includeInactive
  ) {
    if (!includeInactive && !parent.gameObject.active)
      return;
    output.AddRange(parent.GetComponents<T>());
    for (var i = 0; i < parent.childCount; i++)
      FindDescendantComponentsOfType(
          ref output, parent.GetChild(i), includeInactive
      );
  }

  public static IEnumerable<Transform>
  AllDescendantTransforms(Transform parent, bool includeInactive) {
    var stack = new Stack<(Transform, int)>();
    stack.Push((parent, 0));
    while (stack.Count > 0) {
      var (t, i) = stack.Pop();
      if (i < t.childCount) {
        var child = t.GetChild(i);
        if (includeInactive || child.gameObject.active)
          yield return child;
        stack.Push((t, i + 1));
        stack.Push((child, 0));
      }
    }
  }

  public static IEnumerable<T>
  AllDescendantComponents<T>(Transform parent, bool includeInactive)
      where T : Component {
    foreach (var transform in AllDescendantTransforms(
                 parent, includeInactive
             )) {
      foreach (var component in transform.GetComponents<T>()) {
        yield return component;
      }
    }
  }

  public static GameObject[] FindAllInDescendants(
      GameObject root, string name
  ) {
    var result = new List<GameObject>();
    _FindAllInDescendants(root.transform, name, ref result);
    return result.ToArray();
  }
  private static void _FindAllInDescendants(
      Transform transform, string name, ref List<GameObject> result
  ) {
    if (transform.name == name)
      result.Add(transform.gameObject);
    for (int i = 0, count = transform.childCount; i < count; i++)
      _FindAllInDescendants(transform.GetChild(i), name, ref result);
  }

  public static GameObject[] ChildrenToArray(GameObject parent) {
    var children = new List<GameObject>();
    for (var i = 0; i < parent.transform.childCount; i++)
      children.Add(parent.transform.GetChild(i).gameObject);
    return children.ToArray();
  }

  public static IEnumerable<GameObject> RootObjects() {
    for (var i = 0; i < SceneManager.sceneCount; i++) {
      var scene = SceneManager.GetSceneAt(i);
      if (!scene.isLoaded)
        continue;
      foreach (var rootObject in scene.GetRootGameObjects()) {
        yield return rootObject;
      }
    }
  }

  public static Color GenerateColor(int i
  ) => Color.HSVToRGB(i * 0.064f % 1f, 0.9f - i / 16 * 0.3f % 0.8f, 0.9f);

  public static Shader FindShader(string name
  ) => UnityEngine.Resources.FindObjectsOfTypeAll<Shader>()
           .First(shader => shader.name == name);
}
}
