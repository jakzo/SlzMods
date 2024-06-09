using System.Linq;
using UnityEngine;

#if ML6
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Sst.Utilities {
public class Bonelab {
  private static Shader _highlightShader;
  public static Shader HighlightShader {
    get => _highlightShader ??
           (_highlightShader = Resources.FindObjectsOfTypeAll<Shader>().First(
                shader => shader.name == "SLZ/Highlighter"));
  }

  public static void DockToWrist(GameObject gameObject,
                                 bool rightHand = false) {
    var physicsRig = LevelHooks.RigManager.physicsRig;
    var hand = rightHand ? physicsRig.rightHand : physicsRig.leftHand;
    gameObject.transform.SetParent(hand.transform);
    gameObject.transform.localPosition = new Vector3(-0.31f, 0.3f, 0f);
    gameObject.transform.localRotation = Quaternion.Euler(32f, 4f, 3f);
  }

  public static TextMeshPro CreateTextOnWrist(string name,
                                              bool rightHand = false) {
    var text = new GameObject(name);
    var tmp = text.AddComponent<TextMeshPro>();
    tmp.alignment = TextAlignmentOptions.BottomRight;
    tmp.fontSize = 0.5f;
    tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
    DockToWrist(text, rightHand);
    return tmp;
  }
}
}
