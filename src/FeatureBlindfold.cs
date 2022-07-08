using MelonLoader;
using UnityEngine;

namespace SpeedrunTools
{
  class FeatureBlindfold : Feature
  {
    private const float FADE_TIME = 0.15f;

    static public bool s_isBlindfolded = false;

    static private void SetBlindfold(bool blindfolded)
    {
      var compositor = Valve.VRRenderingPackage.OpenVR.Compositor;
      if (compositor == null)
        throw new System.Exception("Failed to blindfold (could not find Steam OpenVR compositor)");
      compositor.FadeToColor(FADE_TIME, 0, 0, 0, blindfolded ? 1 : 0, false);
      s_isBlindfolded = blindfolded;
    }

    public readonly Hotkey HotkeyBlindfold = new Hotkey()
    {
      Predicate = (cl, cr) => Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.B),
      Handler = () =>
      {
        if (s_isBlindfolded)
        {
          MelonLogger.Msg("Unblindfolding");
          SetBlindfold(false);
        } else
        {
          MelonLogger.Msg("Blindfolding");
          SetBlindfold(true);
        }
      }
    };

    bool prevSceneLoading = false;
    public override void OnUpdate()
    {
      if (SceneLoader.loading == prevSceneLoading) return;
      prevSceneLoading = SceneLoader.loading;
      if (!s_isBlindfolded || SceneLoader.loading) return;
      Utils.LogDebug("Blindfolding on scene load");
      SetBlindfold(true);
    }
  }
}
