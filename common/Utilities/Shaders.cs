using System.Linq;
using UnityEngine;

namespace Sst.Utilities {
public class Shaders {
  private static Shader _defaultShader;
  public static Shader DefaultShader {
    get => _defaultShader ??
           (_defaultShader = Resources.FindObjectsOfTypeAll<Shader>().First(
                shader => shader.name == "Sprites/Default"));
  }

  private static Shader _highlightShader;
  public static Shader HighlightShader {
    get => _highlightShader ??
           (_highlightShader = Resources.FindObjectsOfTypeAll<Shader>().First(
                shader => shader.name == "SLZ/Highlighter"));
  }
}
}
