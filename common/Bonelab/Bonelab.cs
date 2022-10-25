using System.Linq;
using UnityEngine;
using SLZ.Rig;

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
}
}
