using MelonLoader;
using UnityEngine;

namespace SpeedrunTools {
class FeatureBlindfold : Feature {
  private const float FADE_TIME = 0.15f;

  static public bool s_isBlindfolded = false;

  static private void SetBlindfold(bool blindfolded,
                                   float fadeTime = FADE_TIME) {
    var compositor = Valve.VRRenderingPackage.OpenVR.Compositor;
    if (compositor == null)
      throw new System.Exception(
          "Failed to blindfold (could not find Steam OpenVR compositor)");
    compositor.FadeToColor(fadeTime, 0, 0, 0, blindfolded ? 1 : 0, false);
    s_isBlindfolded = blindfolded;
  }

  public readonly Hotkey HotkeyBlindfold =
      new Hotkey() { Predicate = (cl, cr) =>
                         Utils.GetKeyControl() && Input.GetKey(KeyCode.B),
                     Handler = () => {
                       if (s_isBlindfolded) {
                         MelonLogger.Msg("Unblindfolding");
                         SetBlindfold(false);
                       } else {
                         MelonLogger.Msg("Blindfolding");
                         SetBlindfold(true);
                       }
                     } };

  private bool _isSceneInited = false;

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    _isSceneInited = true;
  }

  public override void OnUpdate() {
    if (SceneLoader.loading) {
      // I don't know the exact point the scene fades in but it's somewhere
      // between scene init and loading finished so just spam an instant
      // fade-to-black for this period
      if (s_isBlindfolded && _isSceneInited)
        SetBlindfold(true, 0);
    } else {
      _isSceneInited = false;
    }
  }
}
}
