using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sst.Utilities {
public class Unity {
  public static Transform FindDescendantTransform(Transform transform,
                                                  string name) {
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

  public static void FindDescendantComponentsOfType<T>(ref List<T> output,
                                                       Transform parent) {
    output.AddRange(parent.GetComponents<T>());
    for (var i = 0; i < parent.childCount; i++)
      FindDescendantComponentsOfType(ref output, parent.GetChild(i));
  }

  public static GameObject[] FindAllInDescendants(GameObject root,
                                                  string name) {
    var result = new List<GameObject>();
    _FindAllInDescendants(root.transform, name, ref result);
    return result.ToArray();
  }
  private static void _FindAllInDescendants(Transform transform, string name,
                                            ref List<GameObject> result) {
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

  public static Color GenerateColor(int i) =>
      Color.HSVToRGB(i * 0.064f, 0.9f - i / 16 * 0.3f % 0.8f, 0.9f);
}
}
