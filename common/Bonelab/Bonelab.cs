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
                                 RigManager rigManager = null,
                                 bool rightHand = false) {
    if (!rigManager)
      rigManager = GetRigManager();
    var hand = rightHand ? rigManager.physicsRig.rightHand
                         : rigManager.physicsRig.leftHand;
    gameObject.transform.SetParent(hand.transform);
    gameObject.transform.localPosition = new Vector3(-0.31f, 0.3f, 0f);
    gameObject.transform.localRotation = Quaternion.Euler(32f, 4f, 3f);
  }

  public static TextMeshPro CreateTextOnWrist(string name,
                                              bool rightHand = false) {
    var text = new GameObject(name);
    var tmp = text.AddComponent<TMPro.TextMeshPro>();
    tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
    tmp.fontSize = 0.5f;
    tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
    DockToWrist(text, null, rightHand);
    return tmp;
  }
}
}
