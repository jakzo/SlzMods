using System.Linq;
using UnityEngine;
using SLZ.Rig;
using TMPro;

namespace Sst.Utilities {
public class Bonelab {
  public static RigManager GetRigManager() =>
      GameObject.Find("[RigManager (Blank)]")?.GetComponent<RigManager>();

  private static Shader _highlightShader;
  public static Shader HighlightShader {
    get => _highlightShader ??
           (_highlightShader = Resources.FindObjectsOfTypeAll<Shader>().First(
                shader => shader.name == "SLZ/Highlighter"));
  }

  public static void DockToWrist(GameObject gameObject,
                                 Vector3 positionOffset = new Vector3(),
                                 RigManager rigManager = null) {
    if (!rigManager)
      rigManager = GetRigManager();
    gameObject.transform.SetParent(
        rigManager.ControllerRig.leftController.transform);
    gameObject.transform.localPosition = new Vector3(-0.36f, 0.24f, 0f);
    gameObject.transform.localRotation = Quaternion.Euler(46f, 356f, 3f);
    gameObject.transform.localPosition +=
        gameObject.transform.localRotation * positionOffset;
  }

  public static TextMeshPro
  CreateTextOnWrist(string name, Vector3 positionOffset = new Vector3()) {
    var text = new GameObject(name);
    var tmp = text.AddComponent<TMPro.TextMeshPro>();
    tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
    tmp.fontSize = 0.5f;
    tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
    DockToWrist(text, positionOffset);
    return tmp;
  }
}
}
