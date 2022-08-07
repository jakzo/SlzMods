using MelonLoader;
using UnityEngine;

namespace SpeedrunTools.Features {
public class Blindfold : Feature {
  public static Blindfolder s_blindfolder = new Blindfolder();

  public readonly Hotkey HotkeyBlindfold =
      new Hotkey() { Predicate = (cl, cr) => Mod.GameState.currentSceneIdx !=
                                                 Utils.SCENE_MENU_IDX &&
                                             Utils.GetKeyControl() &&
                                             Input.GetKey(KeyCode.B),
                     Handler = () => {
                       if (s_blindfolder.IsBlindfolded) {
                         MelonLogger.Msg("Unblindfolding");
                         s_blindfolder.SetBlindfold(false);
                       } else {
                         MelonLogger.Msg("Blindfolding");
                         s_blindfolder.SetBlindfold(true);
                       }
                     } };

  public override void OnUpdate() { s_blindfolder.OnUpdate(); }

  public override void OnDisabled() { s_blindfolder.SetBlindfold(false); }
}

public class Blindfolder {
  private const float FADE_TIME = 0.15f;

  public bool IsBlindfolded = false;

  private bool _isInited = false;

  public void SetBlindfold(bool blindfolded, float fadeTime = FADE_TIME) {
    var compositor = Valve.VRRenderingPackage.OpenVR.Compositor;
    if (compositor == null)
      throw new System.Exception(
          "Failed to blindfold (could not find Steam OpenVR compositor)");
    compositor.FadeToColor(fadeTime, 0, 0, 0, blindfolded ? 1 : 0, false);
    IsBlindfolded = blindfolded;
  }

  public void OnUpdate() {
    if (IsBlindfolded && SceneLoader.loading) {
      // I don't know the exact point the scene fades in but it's somewhere
      // between scene init and loading finished so just spam an instant
      // fade-to-black for this period
      SetBlindfold(true, 0);
    }
  }
}
}
